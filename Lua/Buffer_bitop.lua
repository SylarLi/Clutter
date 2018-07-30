local ffi = require("ffi")
local C = ffi.C
ffi.cdef[[
void *malloc(size_t __size);
void free(void *__ptr);
]]
local band = bit.band
local bor = bit.bor
local lshift = bit.lshift
local rshift = bit.rshift
local huge = math.huge
local floor = math.floor
local frexp = math.frexp
local ldexp = math.ldexp

Buffer = Buffer or {}
Buffer.__index = Buffer
local ljproto = string.dump(function() end)
Buffer.IsLittleEndian = string.byte(ljproto, 5) % 2 == 0

--- ffi字节流
-- 字节间的排列方式为BigEndian，字节内同lua字节序
-- lua number默认为64位浮点数，读写浮点数建议使用Read(Write)Double
-- Contructor: (ByteLength: 字节长度) or (String: 字符串) or (Buffer: 制作拷贝)
-- 注1：位操作只支持int32，超过2^31强行使用位操作会发生错误(例如：lshfit(0x80, 24) == -2147483648)
-- 注2：单精度浮点数只支持精确到小数点后6位，双精度同ieee754 double precision
Buffer.New = function(...)
    local instance = setmetatable({}, Buffer)
    local args = {...}
    if type(args[1]) == 'number' then
        instance.length = args[1]
        instance.pointer = ffi.cast('uint8_t *', ffi.gc(C.malloc(instance.length), C.free))
        instance.position = 0
    elseif type(args[1]) == 'string' then
        instance.length = #args[1] + 1
        instance.pointer = ffi.cast('uint8_t *', ffi.gc(C.malloc(instance.length), C.free))
        ffi.copy(instance.pointer, args[1])
        instance.position = 0
    elseif type(args[1] == 'table') and getmetatable(args[1]) == Buffer then
        instance.length = #args[1]
        instance.pointer = ffi.cast('uint8_t *', ffi.gc(C.malloc(instance.length), C.free))
        ffi.copy(instance.pointer, args[1].pointer, instance.length)
        instance.position = 0
    else
        error('invalid arguments.')
    end
    return instance
end

function Buffer:__len()
    return self.length
end

function Buffer:ToString()
    return ffi.string(self.pointer, self.length)
end

function Buffer:GetPosition()
    return self.position
end

function Buffer:SetPosition(value)
    assert(value < self.length)
    self.position = value
end

function Buffer:ReadUInt8()
    assert(self.position <= self.length - 1)
    local value = self.pointer[self.position]
    self.position = self.position + 1
    return value
end

function Buffer:WriteUInt8(value)
    assert(type(value) == 'number' and value >= 0 and value < 256)
    assert(self.position <= self.length - 1)
    self.pointer[self.position] = value
    self.position = self.position + 1
end

function Buffer:ReadInt8()
    local value = self:ReadUInt8()
    if value >= 128 then
        value = 128 - value
    end
    return value
end

function Buffer:WriteInt8(value)
    if value < 0 then
        value = -value + 128
    end
    self:WriteUInt8(value)
end

function Buffer:ReadUInt16()
    assert(self.position <= self.length - 2)
    local value = lshift(self.pointer[self.position], 8)
        + self.pointer[self.position + 1]
    self.position = self.position + 2
    return value
end

function Buffer:WriteUInt16(value)
    assert(type(value) == 'number' and value >= 0 and value < 65536)
    assert(self.position <= self.length - 2)
    self.pointer[self.position] = rshift(value, 8)
    self.pointer[self.position + 1] = value
    self.position = self.position + 2
end

function Buffer:ReadInt16()
    local value = self:ReadUInt16()
    if value >= 32768 then
        value = 32768 - value
    end
    return value
end

function Buffer:WriteInt16(value)
    if value < 0 then
        value = -value + 32768
    end
    self:WriteUInt16(value)
end

function Buffer:ReadUInt32()
    assert(self.position <= self.length - 4, 'out of memory.')
    local value = self.pointer[self.position] * 0x1000000
        + self.pointer[self.position + 1] * 0x10000
        + self.pointer[self.position + 2] * 0x100
        + self.pointer[self.position + 3]
    self.position = self.position + 4
    return value
end

function Buffer:WriteUInt32(value)
    assert(type(value) == 'number' and value >= 0 and value < 4294967296, 'invalid uint_32 number.')
    assert(self.position <= self.length - 4, 'out of memory.')
    self.pointer[self.position] = floor(value / 0x1000000)
    self.pointer[self.position + 1] = floor(value / 0x10000) % 0x100
    self.pointer[self.position + 2] = floor(value / 0x100) % 0x100
    self.pointer[self.position + 3] = value % 0x100
    self.position = self.position + 4
end

function Buffer:ReadInt32()
    local value = self:ReadUInt32()
    if value >= 2147483648 then
        value = 2147483648 - value
    end
    return value
end

function Buffer:WriteInt32(value)
    if value < 0 then
        value = -value + 2147483648
    end
    self:WriteUInt32(value)
end

function Buffer:ReadFloat()
    assert(self.position <= self.length - 4)
    local b1, b2, b3, b4 = self.pointer[self.position],
        self.pointer[self.position + 1],
        self.pointer[self.position + 2],
        self.pointer[self.position + 3]
    local sign = b1 > 0x7F and -1 or 1
    local expo = (b1 % 0x80) * 0x2 + floor(b2 / 0x80)
    local mant = ((b2 % 0x80) * 0x100 + b3) * 0x100 + b4
    local value
    if mant == 0 and expo == 0 then
        value = sign * 0.0
    elseif expo == 0xFF then
        if mant == 0 then
            value = sign * huge
        else
            value = 0.0 / 0.0
        end
    else
        value = sign * ldexp(1.0 + mant / 0x800000, expo - 0x7F)
        -- need rounding to even
    end
    self.position = self.position + 4
    return value
end

function Buffer:WriteFloat(value)
    assert(type(value) == 'number')
    assert(self.position <= self.length - 4)
    local sign = 0
    if value < 0.0 then
        sign = 0x80
        value = -value
    end
    local mant, expo = frexp(value)
    if mant ~= mant then
        -- nan
        self.pointer[self.position] = 0xFF
        self.pointer[self.position + 1] = 0x88
        self.pointer[self.position + 2] = 0
        self.pointer[self.position + 3] = 0
    elseif mant == huge or expo > 0x80 then
        if sign == 0 then
            -- inf
            self.pointer[self.position] = 0x7F
        else
            -- -inf
            self.pointer[self.position] = 0xFF
        end
        self.pointer[self.position + 1] = 0x80
        self.pointer[self.position + 2] = 0
        self.pointer[self.position + 3] = 0
    elseif (mant == 0.0 and expo == 0) or expo < -0x7E then
        -- zero
        self.pointer[self.position] = sign
        self.pointer[self.position + 1] = 0
        self.pointer[self.position + 2] = 0
        self.pointer[self.position + 3] = 0
    else
        expo = expo + 0x7E
        mant = floor(ldexp((mant * 2.0 - 1.0), 23) + 0.5)
        self.pointer[self.position] = sign + floor(expo / 0x2)
        self.pointer[self.position + 1] = (expo % 0x2) * 0x80 + floor(mant / 0x10000)
        self.pointer[self.position + 2] = floor(mant / 0x100) % 0x100
        self.pointer[self.position + 3] = mant % 0x100
    end
    self.position = self.position + 4
end

function Buffer:ReadDouble()
    assert(self.position <= self.length - 8)
    local b1, b2, b3, b4, b5, b6, b7, b8 = self.pointer[self.position],
        self.pointer[self.position + 1],
        self.pointer[self.position + 2],
        self.pointer[self.position + 3],
        self.pointer[self.position + 4],
        self.pointer[self.position + 5],
        self.pointer[self.position + 6],
        self.pointer[self.position + 7]
    local sign = b1 > 0x7F and -1 or 1
    local expo = (b1 % 0x80) * 0x10 + floor(b2 / 0x10)
    local mant = ((((((b2 % 0x10) * 0x100 + b3) * 0x100 + b4) * 0x100 + b5) * 0x100 + b6) * 0x100 + b7) * 0x100 + b8
    local value
    if mant == 0 and expo == 0 then
        value = sign * 0.0
    elseif expo == 0x7FF then
        if mant == 0 then
            value = sign * huge
        else
            value = 0.0 / 0.0
        end
    else
        value = sign * ldexp(1.0 + mant / 4503599627370496.0, expo - 0x3FF)
    end
    self.position = self.position + 8
    return value
end

function Buffer:WriteDouble(value)
    assert(type(value) == 'number')
    assert(self.position <= self.length - 8)
    local value1, value2
    local sign = 0
    if value < 0.0 then
        sign = 0x80
        value = -value
    end
    local mant, expo = frexp(value)
    if mant ~= mant then
        -- nan
        self.pointer[self.position] = 0xFF
        self.pointer[self.position + 1] = 0xF8
        self.pointer[self.position + 2] = 0
        self.pointer[self.position + 3] = 0
        self.pointer[self.position + 4] = 0
        self.pointer[self.position + 5] = 0
        self.pointer[self.position + 6] = 0
        self.pointer[self.position + 7] = 0
    elseif mant == huge or expo > 0x400 then
        if sign == 0 then
            -- inf
            self.pointer[self.position] = 0x7F
        else
            -- -inf
            self.pointer[self.position] = 0xFF
        end
        self.pointer[self.position + 1] = 0xF0
        self.pointer[self.position + 2] = 0
        self.pointer[self.position + 3] = 0
        self.pointer[self.position + 4] = 0
        self.pointer[self.position + 5] = 0
        self.pointer[self.position + 6] = 0
        self.pointer[self.position + 7] = 0
    elseif (mant == 0.0 and expo == 0) or expo < -0x3FE then
        -- zero
        self.pointer[self.position] = sign
        self.pointer[self.position + 1] = 0
        self.pointer[self.position + 2] = 0
        self.pointer[self.position + 3] = 0
        self.pointer[self.position + 4] = 0
        self.pointer[self.position + 5] = 0
        self.pointer[self.position + 6] = 0
        self.pointer[self.position + 7] = 0
    else
        expo = expo + 0x3FE
        mant = floor(ldexp((mant * 2.0 - 1.0), 52))
        self.pointer[self.position] = sign + floor(expo / 0x10)
        self.pointer[self.position + 1] = (expo % 0x10) * 0x10 + floor(mant / 0x1000000000000)
        self.pointer[self.position + 2] = floor(mant / 0x10000000000) % 0x100
        self.pointer[self.position + 3] = floor(mant / 0x100000000) % 0x100
        self.pointer[self.position + 4] = floor(mant / 0x1000000) % 0x100
        self.pointer[self.position + 5] = floor(mant / 0x10000) % 0x100
        self.pointer[self.position + 6] = floor(mant / 0x100) % 0x100
        self.pointer[self.position + 7] = mant % 0x100
    end
    self.position = self.position + 8
end

function Buffer:ReadString()
    local value = ffi.string(self.pointer + self.position)
    self.position = self.position + #value + 1
    return value
end

function Buffer:WriteString(value)
    assert(type(value) == 'string')
    assert(self.position <= self.length - (#value + 1))
    ffi.copy(self.pointer + self.position, value)
    self.position = self.position + #value + 1
end

Buffer.UnitTest = function()
    local c1 = collectgarbage('count')
    local t1 = os.clock()

    local buffer = Buffer.New(1024 * 1024)

    -- Test Int8
    local values = { -127, -56, -0, 0, 56, 127 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteInt8(values[i])
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        assert(values[i] == buffer:ReadInt8())
    end

    -- Test UInt8
    values = { 0, 66, 127, 128, 200, 255 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteUInt8(values[i])
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        assert(values[i] == buffer:ReadUInt8())
    end

    -- Test Int16
    values = { -32767, -11156, -0, 0, 11111, 32767 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteInt16(values[i])
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        assert(values[i] == buffer:ReadInt16())
    end

    -- Test UInt16
    values = { 0, 11112, 23568, 32767, 56789, 65535 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteUInt16(values[i])
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        assert(values[i] == buffer:ReadUInt16())
    end

    -- Test Int32
    values = { -2147483647, -1147483647, -0, 0, 1147483647, 2147483647 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteInt32(values[i])
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        assert(values[i] == buffer:ReadInt32())
    end

    -- Test UInt32
    values = { 0, 11112, 2356118, 327671235, 2147483647, 4294967000, 4294967039, 4294967040 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteUInt32(values[i])
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        assert(values[i] == buffer:ReadUInt32())
    end

    values = { 1.1 } --{ -1.7e38, -1.22e16, 1.111111, 0, 0.0, -0.0, 2.999999, -9.43e17, 1.7e38 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteFloat(i)
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        -- buffer:ReadFloat()
        assert(values[i] == buffer:ReadFloat())
    end

    values = { 12345678901234567 } --{ -3.4e38, -1.22e16, 1.111111, 0, 0.0, -0.0, 2.999999, -9.43e17, 3.4e38 }
    buffer:SetPosition(0)
    for i = 1, #values do
        buffer:WriteDouble(values[i])
    end
    buffer:SetPosition(0)
    for i = 1, #values do
        print(buffer:ReadDouble())
        -- assert(values[i] == buffer:ReadDouble())
    end

    -- Test String
    local string = '是阿斯顿1发2生地方asdfasdf,12437-09=-09871!@#$%^&*(O_+\n\t\r\\u1234\\u1\\u890'
    buffer:SetPosition(0)
    buffer:WriteString(string)
    buffer:SetPosition(0)
    assert(buffer:ReadString() == string)

    local c2 = collectgarbage('count')
    local t2 = os.clock()
    print('gc: ' .. (c2 - c1))
    print('time: ' .. (t2 - t1))
    print('pass.')
end