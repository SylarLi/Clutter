Sugarcoat = {}
local this = Sugarcoat

this.UnitTest = false
this.Debug = false

this.Cheating = false

--- 重写Clone
Clone = function(object)
    local lookup_table = {}
    local function _copy(object)
        if type(object) ~= "table" then
            return object
        elseif lookup_table[object] then
            return lookup_table[object]
        end
        local new_table = {}
        local metatable = getmetatable(object)
        if object.__tname == 'Sugarcoat' then
            if metatable ~= nil then
                setmetatable(new_table, getmetatable(metatable))
            end
            new_table = Sugarcoat.New(new_table)
        else
            setmetatable(new_table, metatable)
        end
        lookup_table[object] = new_table
        for key, value in pairs(object) do
            new_table[_copy(key)] = _copy(value)
        end
        return new_table
    end
    return _copy(object)
end

--- 重写pairs
local lua_pairs = pairs
pairs = function(t)
    local mt = getmetatable(t)
    if mt and mt.__pairs then
        return mt.__pairs(t)
    else
        return lua_pairs(t)
    end
end

--- 重写ipairs
local lua_ipairs = ipairs
ipairs = function(t)
    local mt = getmetatable(t)
    if mt and mt.__ipairs then
        return mt.__ipairs(t)
    else
        return lua_ipairs(t)
    end
end

--- 加密table, 用法和table一致，初始化和设值时会递归将table修改为加密table
-- 构建加密table之后，访问修改原table不会影响加密table
-- 调用刷新加密secret: instance.__flush()
-- 只有在取值的时候进行校验，设值和遍历不会
-- metatable不会加密
-- 数组(#x > 0)由于无法重写#，故不做保护
function this.New(data)
    local markup = {}
    local function construct(data)
        assert(data == nil or type(data) == 'table', 'data should be a table or nil.')
        if data and (#data > 0 or data.__tname == 'Sugarcoat') then return data end
        local proxy = {}
        local instance = {}
        local cryptos = {}
        local secret = this.RandomSecret()
        local mtm = {}
        local get = function(k)
            local v = rawget(instance, k)
            local c = cryptos[k]
            local d = this.Decrypt(v, c, secret)
            if v ~= d then
                this.Cheating = true
                print('Cheating -- ' .. v .. ' : ' .. d)
                Debugger.LogError('Cheating!!!')
            end
            return v
        end
        local set = function(k, v)
            if v ~= nil and
                type(v) == 'table' and
                v.__tname ~= 'Sugarcoat' then
                v = construct(v)
            end
            local c = this.Encrypt(v, secret)
            cryptos[k] = c
            rawset(instance, k, v)
        end
        local gen = function()
            secret = this.RandomSecret()
            for k, v in pairs(instance) do
                set(k, v)
            end
        end
        local genv = function()
            for k, v in pairs(instance) do
                get(k, v)
            end
            gen()
        end
        local mtt = {
            __index = function(t, k)
                if k == '__tname' then
                    return 'Sugarcoat'
                elseif k == '__flush' then
                    return genv
                else
                    local v = get(k)
                    if v ~= nil then
                        return v
                    elseif mtm.__index ~= nil then
                        return mtm.__index(data, k)
                    else
                        return nil
                    end
                end
            end,
            __newindex = function(t, k, v)
                set(k, v)
            end,
            __pairs = function(t)
                return lua_pairs(instance)
            end,
            __ipairs = function(t)
                return lua_ipairs(instance)
            end
        }
        setmetatable(proxy, mtt)
        if data ~= nil then
            local dmt = getmetatable(data)
            if dmt ~= nil then
                local pmethods = { '__index' }
                for _, method in ipairs(pmethods) do
                    local it = type(dmt[method])
                    if it == 'table' then
                        mtm[method] = function(t, k)
                            return dmt[method][k]
                        end
                    elseif it == 'function' then
                        mtm[method] = dmt[method]
                    end
                end
                local fmethods = { '__tostring', '__div', '__mul', '__add', '__sub', '__unm', '__eq' }
                for _, method in ipairs(fmethods) do
                    local it = type(dmt[method])
                    if it == 'table' then
                        mtt[method] = function(...)
                            return dmt[method][k](...)
                        end
                    elseif it == 'function' then
                        mtt[method] = function(...)
                            return dmt[method](...)
                        end
                    end
                end
                setmetatable(mtt, dmt)
            end
            markup[data] = proxy
            for k, v in pairs(data) do
                if markup[v] then
                    rawset(instance, k, markup[data])
                else
                    set(k, v)
                end
            end
        end
        return proxy
    end
    return construct(data)
end

function this.Encrypt(raw, secret)
    local ret = nil
    if raw ~= nil then
        local t = type(raw)
        if t == 'number' then
            if raw == math.floor(raw) then
                ret = bit.bxor(raw, secret)
            else
                ret = secret .. raw
            end
        elseif t == 'string' then
            ret = secret .. raw
        elseif t == 'boolean' then
            ret = not raw
        else
            ret = tostring(raw) .. secret
        end
    end
    if this.Debug then
        Debugger.LogError(tostring(raw) .. ' E--> ' .. tostring(ret))
    end
    return ret
end

function this.Decrypt(raw, crypto, secret)
    local ret = nil
    if raw ~= nil then
        local t = type(raw)
        if t == 'number' then
            if raw == math.floor(raw) then
                ret = bit.bxor(crypto, secret)
            else
                ret = tonumber(string.sub(crypto, tostring(secret):len() + 1))
            end
        elseif t == 'string' then
            ret = string.sub(crypto, tostring(secret):len() + 1)
        elseif t == 'boolean' then
            ret = not crypto
        else
            ret = (tostring(raw) .. secret) == crypto and raw or nil
        end
    end
    if this.Debug then
        Debugger.LogError(tostring(crypto) .. ' D--> ' .. tostring(ret))
    end
    return ret
end

function this.RandomSecret()
    return math.floor(math.random() * 2147483647)
end

if not this.UnitTest then return end

-- 测试用例
local raw = {
    a = 'aa',
    b = 'bb',
    c = 'cc',
    d = 1,
    e = 1.1
}
local data = Sugarcoat.New(raw)
assert(data.d == 1)
assert(data.e == 1.1)
data.a = 'dd'
assert(data.a == 'dd')
raw.a = 'ee'
assert(data.a == 'dd')

data = Sugarcoat.New()
data.a = 'dd'
assert(data.a == 'dd')
data.a = 'ee'
assert(data.a == 'ee')

data = Sugarcoat.New()
assert(data.__tname == 'Sugarcoat')

local verify
verify = function(t1, t2)
    if type(t1) ~= 'table' or
        type(t2) ~= 'table' then
        assert(t1 == t2, tostring(t1) .. ' | ' .. tostring(t2))
    else
        for k, v in pairs(t1) do
            if type(v) ~= 'table' or v ~= t1 then
                verify(t1[k], t2[k])
            end
        end
    end
end

Equipment = import('model.vo.Equipment')
local equip = Equipment.New({
    id = 46244,
    count = 1
})
data = Sugarcoat.New(equip)
verify(data, equip)

local a = {
    r = 1
}
local b = {
    __index = function(t, k)
        return 2
    end
}
setmetatable(a, b)
data = Sugarcoat.New(a)
assert(a.x == 2)
assert(data.x == 2)
data.x = 3
assert(data.x == 3)

local e1 = Sugarcoat.New(Equipment.New({
    id = 46244,
    count = 1,
}))


local et = e1:clone()
et.config.name = '2'
assert(et.config.name == et:clone().config.name)

assert(this.Cheating == false)