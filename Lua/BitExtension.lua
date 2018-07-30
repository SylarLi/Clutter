--- Primitive序列化为字节流

local band = bit.band
local bor = bit.bor
local lshift = bit.lshift
local rshift = bit.rshift
local huge = math.huge
local floor = math.floor
local frexp = math.frexp
local ldexp = math.ldexp

local ljproto = string.dump(function() end)
bit.littleendian = string.byte(ljproto, 5) % 2 == 0

function bit.newarray()
    return {
        data = {},
        index = 0,
        length = 0,
        anchor = -1,
    }
end

function bit.checktop(array)
    if array.anchor < 0 then
        if array.length == #array.data then
            for i = 1, 256 do
                table.insert(array.data, 0x00000000)
            end
        end
        if array.index == array.length then
            array.length = array.length + 1
        end
        array.index = array.index + 1
        array.anchor = 3
    end
end

function bit.writevalue(array, value, bnums)
    if array.anchor >= 0 and array.anchor < bums - 1 then
        local rnums = bnums - 1 - array.anchor
        array.data[array.index] = bor(array.data[array.index], rshift(value, rnums * 8))
        array.anchor = -1
        bit.writevalue(array, value, rnums)
    else
        bit.checktop(array)
        array.data[array.index] = bor(array.data[array.index], lshift(value, (array.anchor + 1 - bnums) * 8))
        array.anchor = array.anchor - bnums
    end
end

function bit.writebyte(array, byte)
    if byte < 0 then
        byte = -byte + 128
    end
    bit.writevalue(array, byte, 1)
end

function bit.writeshort(array, short)
    if short < 0 then
        short = -short + 32768
    end
    bit.writevalue(array, short, 2)
end

function bit.writeint(array, int)
    if int < 0 then
        int = -int + 2147483648
    end
    bit.writevalue(array, int, 4)
end

function bit.writelong(array, long)
    if bit.littleendian then
        bit.writevalue(array, long, 4)
        bit.writevalue(array, rshift(long, 32), 4)
    else
        bit.writevalue(array, rshift(long, 32), 4)
        bit.writevalue(array, long, 4)
    end
end

function bit.writefloat(array, float)
    local value
    local sign = 0
    if float < 0.0 then
        sign = 0x80
        float = -float
    end
    local mant, expo = frexp(float)
    if mant ~= mant then
        value = 0xFF880000              -- nan
    elseif mant == huge or expo > 0x80 then
        if sign == 0 then
            value = 0x7F800000          -- inf
        else
            value = 0xFF800000          -- -inf
        end
    elseif (mant == 0.0 and expo == 0) or expo < -0x7E then
        value = lshift(sign, 24)    -- zero
    else
        expo = expo + 0x7E
        mant = floor((mant * 2.0 - 1.0) * ldexp(0.5, 24))
        value = lshift(sign + floor(expo / 0x2), 24) +
            lshift((expo % 0x2) * 0x80 + floor(mant / 0x10000), 16) +
            lshift(floor(mant / 0x100) % 0x100, 8) +
            mant % 0x100
    end
    bit.writevalue(array, value, 4)
end

function bit.writedouble(array, double)
    local value1, value2
    local sign = 0
    if double < 0.0 then
        sign = 0x80
        double = -double
    end
    local mant, expo = frexp(double)
    if mant ~= mant then
        value1 = 0xFFF80000                 -- nan
        value2 = 0
    elseif mant == math.huge or expo > 0x400 then
        if sign == 0 then
            value1 = 0x7FF00000             -- inf
            value2 = 0
        else
            value1 = 0xFFF00000             -- -inf
            value2 = 0
        end
    elseif (mant == 0.0 and expo == 0) or expo < -0x3FE then
        value1 = lshift(sign, 24)           -- zero
        value2 = 0
    else
        expo = expo + 0x3FE
        mant = floor((mant * 2.0 - 1.0) * ldexp(0.5, 53))
        value1 = lshift(sign + floor(expo / 0x10), 24) +
            lshift((expo % 0x10) * 0x10 + floor(mant / 0x1000000000000), 16) +
            lshift(floor(mant / 0x10000000000) % 0x100, 8) +
            floor(mant / 0x100000000) % 0x100
        value2 = lshift(floor(mant / 0x1000000) % 0x100, 24) +
            lshift(floor(mant / 0x10000) % 0x100, 16) +
            lshift(floor(mant / 0x100) % 0x100, 8) +
            mant % 0x100
    end
    if bit.littleendian then
        bit.writevalue(array, value2, 4)
        bit.writevalue(array, value1, 4)
    else
        bit.writevalue(array, value1, 4)
        bit.writevalue(array, value2, 4)
    end
end

function bit.unittest()
    local c1 = collectgarbage('count')
    local t1 = os.clock()
    local a = bit.newarray()
    for i = 1, 1000000 do --2147483647 do
        bit.writeint(a, i)
    end
    local c2 = collectgarbage('count')
    local t2 = os.clock()
    print('gc: ' .. (c2 - c1))
    print('time: ' .. (t2 - t1))
    local t3 = os.clock()
    print('time: ' .. (t3 - t2))
end