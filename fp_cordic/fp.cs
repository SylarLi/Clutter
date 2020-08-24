using System;
using System.Runtime.CompilerServices;

namespace RRoutine.Common
{
    /// <summary>
    /// Fixed point(31.32)
    /// 精度最多精确到小数点后9位
    /// </summary>
    public struct fp : IComparable, IFormattable, IConvertible, IComparable<fp>, IEquatable<fp>
    {
        public static readonly fp MaxValue = new fp(MaxRaw);
        public static readonly fp MinValue = new fp(MinRaw);
        public static readonly fp Epsilon = new fp(1);

        private const long MaxRaw = long.MaxValue;
        private const long MinRaw = long.MinValue;
        private const int TotalBits = 64;
        private const int FracBits = 32;
        private const long Fraction = 1L << FracBits;
        private const long FracMask = 0x00000000FFFFFFFF;
        private const ulong TotalMask = 0xFFFFFFFFFFFFFFFF;

        public long m_value;

        public fp(long value)
        {
            m_value = value;
        }

        public static implicit operator fp(byte value)
        {
            return new fp((long) value << FracBits);
        }

        public static implicit operator byte(fp value)
        {
            return (byte) (value.m_value >> FracBits);
        }

        public static implicit operator fp(sbyte value)
        {
            return new fp((long) value << FracBits);
        }

        public static implicit operator sbyte(fp value)
        {
            return (sbyte) (value.m_value >> FracBits);
        }

        public static implicit operator fp(ushort value)
        {
            return new fp((long) value << FracBits);
        }

        public static implicit operator ushort(fp value)
        {
            return (ushort) (value.m_value >> FracBits);
        }

        public static implicit operator fp(short value)
        {
            return new fp((long) value << FracBits);
        }

        public static implicit operator short(fp value)
        {
            return (short) (value.m_value >> FracBits);
        }

        public static implicit operator fp(uint value)
        {
            return new fp((long) value << FracBits);
        }

        public static implicit operator uint(fp value)
        {
            return (uint) (value.m_value >> FracBits);
        }

        public static implicit operator fp(int value)
        {
            return new fp((long) value << FracBits);
        }

        public static implicit operator int(fp value)
        {
            return (int) (value.m_value >> FracBits);
        }

        public static implicit operator fp(ulong value)
        {
            return new fp((long) value << FracBits);
        }

        public static implicit operator ulong(fp value)
        {
            return (ulong) (value.m_value >> FracBits);
        }

        public static implicit operator fp(long value)
        {
            return new fp(value << FracBits);
        }

        public static implicit operator long(fp value)
        {
            return value.m_value >> FracBits;
        }

        public static implicit operator fp(float value)
        {
            return new fp((long) (value * Fraction));
        }

        public static implicit operator float(fp value)
        {
            return (float) ((double) value.m_value / Fraction);
        }

        public static implicit operator fp(double value)
        {
            return new fp((long) (value * Fraction));
        }

        public static implicit operator double(fp value)
        {
            return value.m_value / (double) Fraction;
        }

        public static bool operator ==(fp left, fp right)
        {
            return left.m_value == right.m_value;
        }

        public static bool operator !=(fp left, fp right)
        {
            return left.m_value != right.m_value;
        }

        public static bool operator >(fp left, fp right)
        {
            return left.m_value > right.m_value;
        }

        public static bool operator <(fp left, fp right)
        {
            return left.m_value < right.m_value;
        }

        public static bool operator >=(fp left, fp right)
        {
            return left.m_value >= right.m_value;
        }

        public static bool operator <=(fp left, fp right)
        {
            return left.m_value <= right.m_value;
        }

        public static fp operator +(fp left, fp right)
        {
            return new fp(left.m_value + right.m_value);
        }

        public static fp operator -(fp left, fp right)
        {
            return new fp(left.m_value - right.m_value);
        }

        public static fp operator -(fp value)
        {
            return new fp(-value.m_value);
        }

        public static fp operator *(fp left, fp right)
        {
            var l = left.m_value;
            var li = l >> FracBits;
            var lf = (ulong) (l & FracMask);
            var r = right.m_value;
            var ri = r >> FracBits;
            var rf = (ulong) (r & FracMask);
            return new fp((li * ri << FracBits) +
                          (li * (long) rf + ri * (long) lf) +
                          (long) (lf * rf >> FracBits));
        }

        public static fp operator /(fp left, fp right)
        {
            var xl = left.m_value;
            var yl = right.m_value;
            if (yl == 0)
                throw new DivideByZeroException();
            var remainder = (ulong) (xl >= 0 ? xl : -xl);
            var divider = (ulong) (yl >= 0 ? yl : -yl);
            var quotient = 0UL;
            var bitPos = FracBits + 1;
            while ((divider & 0xF) == 0 && bitPos >= 4)
            {
                divider >>= 4;
                bitPos -= 4;
            }

            while (remainder != 0 && bitPos >= 0)
            {
                var shift = CountLeadingZeroes(remainder);
                shift = shift > bitPos ? bitPos : shift;
                remainder <<= shift;
                bitPos -= shift;
                var div = remainder / divider;
                remainder %= divider;
                quotient += div << bitPos;
                if ((div & ~(TotalMask >> bitPos)) != 0)
                    return ((xl ^ yl) & MinRaw) == 0 ? MaxValue : MinValue;
                remainder <<= 1;
                --bitPos;
            }

            ++quotient;
            var result = (long) (quotient >> 1);
            if (((xl ^ yl) & MinRaw) != 0)
                result = -result;
            return new fp(result);
        }

        public static fp operator %(fp left, fp right)
        {
            return new fp(left.m_value % right.m_value);
        }

        public int CompareTo(object value)
        {
            if (value == null)
                return 1;
            if (!(value is fp))
                throw new ArgumentException("Value is not an instance of fp.");
            return CompareTo((fp) value);
        }

        public override string ToString()
        {
            return $"{(double) this:#########0.#########}";
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.IsNullOrEmpty(format)
                ? ToString(formatProvider)
                : string.Format(formatProvider, format, (double) this);
        }

        public TypeCode GetTypeCode()
        {
            throw new NotImplementedException();
        }

        public bool ToBoolean(IFormatProvider provider)
        {
            return m_value != 0;
        }

        public byte ToByte(IFormatProvider provider)
        {
            return this;
        }

        public char ToChar(IFormatProvider provider)
        {
            return Convert.ToChar((float) this, provider);
        }

        public DateTime ToDateTime(IFormatProvider provider)
        {
            return Convert.ToDateTime((float) this, provider);
        }

        public decimal ToDecimal(IFormatProvider provider)
        {
            return Convert.ToDecimal((float) this, provider);
        }

        public double ToDouble(IFormatProvider provider)
        {
            return this;
        }

        public short ToInt16(IFormatProvider provider)
        {
            return this;
        }

        public int ToInt32(IFormatProvider provider)
        {
            return this;
        }

        public long ToInt64(IFormatProvider provider)
        {
            return this;
        }

        public sbyte ToSByte(IFormatProvider provider)
        {
            return this;
        }

        public float ToSingle(IFormatProvider provider)
        {
            return this;
        }

        public string ToString(IFormatProvider provider)
        {
            return string.Format(provider, "{0:#########0.#########}", (double) this);
        }

        public object ToType(Type conversionType, IFormatProvider provider)
        {
            return Convert.ChangeType(this, conversionType, provider);
        }

        public ushort ToUInt16(IFormatProvider provider)
        {
            return this;
        }

        public uint ToUInt32(IFormatProvider provider)
        {
            return this;
        }

        public ulong ToUInt64(IFormatProvider provider)
        {
            return this;
        }

        public int CompareTo(fp other)
        {
            if (this == other)
                return 0;
            return this > other ? 1 : -1;
        }

        public bool Equals(fp other)
        {
            return this == other;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int CountLeadingZeroes(ulong x)
        {
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            x |= x >> 32;
            x -= (x >> 1) & 0x5555555555555555UL;
            x = (x & 0x3333333333333333UL) + ((x >> 2) & 0x3333333333333333UL);
            return TotalBits - (int)(unchecked(((x + (x >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }
    }
}