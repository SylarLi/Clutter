using System.Runtime.CompilerServices;
using UnityEngine;

namespace RRoutine.Common
{
    public static class Mathfp
    {
        /// <summary>
        /// PI
        /// </summary>
        public static readonly fp PI = new fp(PIRaw);

        /// <summary>
        /// E
        /// </summary>
        public static readonly fp E = new fp(ERaw);

        private const int TotalBits = 64;
        private const int FracBits = 32;
        private const ulong IntMask = 0xFFFFFFFF00000000;
        private const long IntOne = 0x0000000100000000;
        private const long FracMask = 0x00000000FFFFFFFF;
        private const long FracHalf = 0x80000000;
        private const int SqrtIterateCnt = FracBits + (FracBits >> 1);
        private const int SqrtShiftLeft = 2;
        private const int SqrtShiftRight = TotalBits - SqrtShiftLeft;
        private const long PIRaw = 0x3243f6a88;
        private const long PIRawDiv2 = 0x1921fb544;
        private const long PIRawMulN = 0x6487ed5110b4611a;
        private const long MaxAtan = 0x4dba76D400000000;
        private const long ERaw = 0x2b7e15162;
        private static readonly fp CORDIC_K = new fp(0x9b74eda6);
        private static readonly fp CORDIC_KH = new fp(0x1351e872c);
        private const long LN2Raw = 0xb17217f7;
        private const long LN2RawMul31 = 0x157cd0e702;
        private const long LN2RawMul16 = 0xb17217f7d;
        private const long HBMax = 0x1193ea7aa;
        private const long LNMax = 0x95a5e353f;
        private const long LNMin = 0x1c28f5c2;

        private static readonly long[] CORDIC_Atan =
        {
            0xc90fdaa2, 0x76b19c15, 0x3eb6ebf2, 0x1fd5ba9a, 0xffaaddb, 0x7ff556e, 0x3ffeaab, 0x1fffd55, 0xffffaa,
            0x7ffff5, 0x3ffffe, 0x1fffff, 0xfffff, 0x7ffff, 0x3ffff, 0x1ffff, 0xffff, 0x7fff, 0x3fff, 0x1fff, 0xfff,
            0x7ff, 0x3ff, 0x1ff, 0xff, 0x7f, 0x3f, 0x20, 0x10, 0x8, 0x4, 0x2, 0x1
        };

        private static readonly long[] CORDIC_Atanh =
        {
            0x0, 0x8c9f53d5, 0x4162bbea, 0x202b1239, 0x1005588a, 0x800aac4, 0x4001556, 0x20002aa, 0x1000055, 0x80000a,
            0x400001, 0x200000, 0x100000, 0x80000, 0x40000, 0x20000, 0x10000, 0x8000, 0x4000, 0x1fff, 0xfff, 0x7ff,
            0x3ff, 0x1ff, 0xff, 0x7f, 0x3f, 0x20, 0xf, 0x7, 0x3, 0x1, 0x0
        };

        public static fp Abs(fp x)
        {
            var t = x.m_value >> TotalBits - 1;
            return new fp((x.m_value ^ t) - t);
        }

        public static fp Floor(fp x)
        {
            return new fp((long) ((ulong) x.m_value & IntMask));
        }

        public static fp Ceiling(fp x)
        {
            return Floor(x) + ((x.m_value & FracMask) == 0 ? 0 : 1);
        }

        public static fp Round(fp x)
        {
            var high = Floor(x);
            var low = x.m_value & FracMask;
            if (low < FracHalf)
                return high;
            if (low > FracHalf)
                return high + 1;
            return high + ((high.m_value & IntOne) == 0 ? 0 : 1);
        }

        public static fp Max(fp x, fp y)
        {
            return x > y ? x : y;
        }

        public static fp Min(fp x, fp y)
        {
            return x < y ? x : y;
        }

        public static fp Clamp(fp x, fp min, fp max)
        {
            x = x < min ? min : x;
            x = x > max ? max : x;
            return x;
        }

        public static fp Sign(fp x)
        {
            if (x > 0)
                return 1;
            if (x < 0)
                return -1;
            return 0;
        }

        public static fp Sqrt(fp x)
        {
            // http://jet.ro/files/The_neglected_art_of_Fixed_Point_arithmetic_20060913.pdf
            // page 21
            var root = 0UL;
            var remHi = 0UL;
            var remLo = (ulong) x.m_value;
            var count = SqrtIterateCnt;
            while (count-- != 0)
            {
                remHi = (remHi << SqrtShiftLeft) | (remLo >> SqrtShiftRight);
                remLo <<= SqrtShiftLeft;
                root <<= 1;
                var testDiv = (root << 1) + 1;
                if (remHi >= testDiv)
                {
                    remHi -= testDiv;
                    root += 1;
                }
            }

            return new fp((long) root);
        }

        public static fp Sin(fp x)
        {
            // 粗略测试最大误差约为1e-7
            var n = NormalizeRadian(x.m_value);
            var p = true;
            if (n > PIRaw)
            {
                n -= PIRaw;
                p = false;
            }
            else if (n < -PIRaw)
            {
                n += PIRaw;
                p = false;
            }

            if (n > PIRawDiv2)
                n = PIRaw - n;
            else if (n < -PIRawDiv2)
                n = -(PIRaw + n);
            // -4 -2 Magic number，使得Sin(0) == 0
            var ux = CORDIC_K.m_value - 4;
            var uy = -2L;
            CORDICCircular(ref n, ref ux, ref uy);
            return new fp(p ? uy : -uy);
        }

        public static fp Cos(fp x)
        {
            // 粗略测试最大误差约为1e-7
            return Sin(new fp(x.m_value + (x.m_value > 0 ? -PIRaw - PIRawDiv2 : PIRawDiv2)));
        }

        public static fp Tan(fp x)
        {
            // 当x接近PI/2或-PI/2时，定点数的精度问题会导致误差被无限放大。
            // 因为在PI/2和-PI/2时正切值不是连续的，在两侧分别趋近于正无穷和负无穷
            var n = NormalizeRadian(x.m_value);
            if (n > PIRaw)
                n -= PIRaw;
            else if (n < -PIRaw)
                n += PIRaw;
            var p = true;
            if (n > PIRawDiv2)
            {
                n = PIRaw - n;
                p = false;
            }
            else if (n < -PIRawDiv2)
            {
                n = -(PIRaw + n);
                p = false;
            }

            var ux = CORDIC_K.m_value - 4;
            var uy = -2L;
            CORDICCircular(ref n, ref ux, ref uy);
            if (uy == 0)
                return 0;
            uy = p ? uy : -uy;
            if (ux == 0)
                return new fp(uy > 0 ? fp.MaxValue : fp.MinValue);
            return new fp(uy) / new fp(ux);
        }

        public static fp Atan(fp x)
        {
            // 粗略测试最大误差约为1e-9
            // 返回值范围[-PI/2, PI/2]
            if (x.m_value >= MaxAtan)
                return new fp(PIRawDiv2);
            if (x.m_value <= -MaxAtan)
                return new fp(-PIRawDiv2);
            var n = 0L;
            var ux = IntOne;
            var uy = x.m_value;
            InvertCORDICCircular(ref n, ref ux, ref uy);
            return new fp(n);
        }

        public static fp Atan2(fp y, fp x)
        {
            // 粗略测试最大误差约为1e-9
            // 返回值范围[-PI, PI]
            var ux = x.m_value;
            var uy = y.m_value;
            if (uy == 0)
                return 0;
            if (ux == 0)
                return new fp(uy > 0 ? PIRawDiv2 : -PIRawDiv2);
            var sx = ux > 0;
            var sy = uy > 0;
            // 特殊处理-long.MinValue
            var nx = sx ? ux : ux == fp.MinValue.m_value ? fp.MaxValue.m_value : -ux;
            var ny = sy ? uy : uy == fp.MinValue.m_value ? fp.MaxValue.m_value : -uy;
            var zx = CountLeadingZeroes((ulong) nx);
            var zy = CountLeadingZeroes((ulong) ny);
            var df = zy - zx;
            if (df >= FracBits)
                return sx ? (fp) 0 : sy ? PI : -PI;
            if (df <= -FracBits)
                return new fp(sy ? PIRawDiv2 : -PIRawDiv2);
            var shift = (zx + zy) / 2 - FracBits;
            if (shift >= 0)
            {
                nx <<= shift;
                uy <<= shift;
            }
            else
            {
                nx >>= -shift;
                uy >>= -shift;
            }

            var n = 0L;
            InvertCORDICCircular(ref n, ref nx, ref uy);
            return new fp(sx ? n : sy ? PIRaw - n : -(PIRaw + n));
        }

        public static fp Asin(fp x)
        {
            // 粗略测试最大误差约为1e-8
            // 返回值范围[-PI/2, PI/2]
            if (x >= 1)
                return new fp(PIRawDiv2);
            if (x <= -1)
                return new fp(-PIRawDiv2);
            return Atan(x / Sqrt(1 - x * x));
        }

        public static fp Acos(fp x)
        {
            // 粗略测试最大误差约为1e-8
            // 返回值范围[0, PI]
            return new fp(PIRawDiv2 - Asin(x).m_value);
        }

        public static fp Sinh(fp x)
        {
            // 因定点数精度有限，abs(x)在接近fp(91986135426)时误差为最大5.98967337608337
            if (x.m_value >= LN2RawMul31)
                return fp.MaxValue;
            if (x.m_value <= -LN2RawMul31)
                return fp.MinValue;
            var sn = x.m_value > 0;
            var n = sn ? x.m_value : -x.m_value;
            var q = 0;
            if (n > HBMax)
                n = NormalizeHyperbolic(n, out q);
            var ux = CORDIC_KH.m_value + 4;
            var uy = 0L;
            CORDICHyperbolic(ref n, ref ux, ref uy);
            var nx = (ulong) (ux > 0 ? ux : -ux);
            var ny = (ulong) (uy > 0 ? uy : -uy);
            ny = (nx + ny << q) - (nx - ny >> q) >> 1;
            uy = (long) ny;
            return new fp(sn ? uy : -uy);
        }

        public static fp Cosh(fp x)
        {
            // 因定点数精度有限，abs(x)在接近fp(91986135426)时误差为最大5.98967337608337
            if (x.m_value >= LN2RawMul31 ||
                x.m_value <= -LN2RawMul31)
                return fp.MaxValue;
            var n = x.m_value > 0 ? x.m_value : -x.m_value;
            var q = 0;
            if (n > HBMax)
                n = NormalizeHyperbolic(n, out q);
            var ux = CORDIC_KH.m_value + 2;
            var uy = 0L;
            CORDICHyperbolic(ref n, ref ux, ref uy);
            var nx = (ulong) (ux > 0 ? ux : -ux);
            var ny = (ulong) (uy > 0 ? uy : -uy);
            nx = (nx + ny << q) + (nx - ny >> q) >> 1;
            return new fp((long) nx);
        }

        public static fp Tanh(fp x)
        {
            // 粗略测试最大误差约为1e-9
            // 返回值为[-1, 1]
            if (x.m_value >= LN2RawMul31)
                return 1;
            if (x.m_value <= -LN2RawMul31)
                return -1;
            var sn = x.m_value > 0;
            var n = sn ? x.m_value : -x.m_value;
            var q = 0;
            if (n > HBMax)
                n = NormalizeHyperbolic(n, out q);
            var ux = CORDIC_KH.m_value + 4;
            var uy = 0L;
            CORDICHyperbolic(ref n, ref ux, ref uy);
            if (ux == 0)
                return 0;
            var nx = (ulong) (ux > 0 ? ux : -ux);
            var ny = (ulong) (uy > 0 ? uy : -uy);
            var tx = (nx + ny << q) + (nx - ny >> q) >> 1;
            var ty = (nx + ny << q) - (nx - ny >> q) >> 1;
            ux = (long) tx;
            uy = (long) ty;
            return new fp(sn ? uy : -uy) / new fp(ux);
        }

        public static fp Pow(fp x)
        {
            if (x.m_value >= LN2RawMul31)
                return fp.MaxValue;
            if (x.m_value <= -LN2RawMul31)
                return 0;
            var n = x.m_value;
            var q = 0;
            if (n > HBMax)
                n = NormalizeHyperbolic(n, out q);
            else if (n < -HBMax)
                n = -NormalizeHyperbolic(-n, out q);
            var ux = CORDIC_KH.m_value + 1;
            var uy = CORDIC_KH.m_value;
            CORDICHyperbolic(ref n, ref ux, ref uy);
            return new fp(n > 0 ? ux << q : ux >> q);
        }

        public static fp Log(fp x)
        {
            // 粗略测试最大误差约为1e-8
            // 返回值为[-ln2 * 31, ln2 * 31]
            var nx = x.m_value;
            if (nx <= 0)
                return 0;
            var e = 0;
            while (nx < LNMin)
            {
                nx <<= 1;
                e -= 1;
            }

            while (nx > LNMax)
            {
                nx >>= 1;
                e += 1;
            }

            var n = 2L;
            var ux = nx + IntOne;
            var uy = nx - IntOne - 1;
            InvertCORDICHyperbolic(ref n, ref ux, ref uy);
            return new fp((n << 1) + LN2Raw * e);
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
            return TotalBits - (int) (unchecked(((x + (x >> 4)) & 0xF0F0F0F0F0F0F0FUL) * 0x101010101010101UL) >> 56);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CORDICCircular(ref long z, ref long x, ref long y)
        {
            for (var i = 0; i <= FracBits; i++)
            {
                var tx = x >> i;
                var ty = y >> i;
                var tz = CORDIC_Atan[i];
                x -= z > 0 ? ty : -ty;
                y += z > 0 ? tx : -tx;
                z -= z > 0 ? tz : -tz;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InvertCORDICCircular(ref long z, ref long x, ref long y)
        {
            for (var i = 0; i <= FracBits; i++)
            {
                var tx = x >> i;
                var ty = y >> i;
                var tz = CORDIC_Atan[i];
                x -= -y > 0 ? ty : -ty;
                z -= -y > 0 ? tz : -tz;
                y += -y > 0 ? tx : -tx;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CORDICHyperbolic(ref long z, ref long x, ref long y)
        {
            for (var i = 1; i <= FracBits; i++)
            {
                var tx = x >> i;
                var ty = y >> i;
                var tz = CORDIC_Atanh[i];
                x += z > 0 ? ty : -ty;
                y += z > 0 ? tx : -tx;
                z -= z > 0 ? tz : -tz;
                if (i == 4 || i == 13)
                {
                    tx = x >> i;
                    ty = y >> i;
                    tz = CORDIC_Atanh[i];
                    x += z > 0 ? ty : -ty;
                    y += z > 0 ? tx : -tx;
                    z -= z > 0 ? tz : -tz;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void InvertCORDICHyperbolic(ref long z, ref long x, ref long y)
        {
            for (var i = 1; i <= FracBits; i++)
            {
                var tx = x >> i;
                var ty = y >> i;
                var tz = CORDIC_Atanh[i];
                x += -y > 0 ? ty : -ty;
                z -= -y > 0 ? tz : -tz;
                y += -y > 0 ? tx : -tx;
                if (i == 4 || i == 13)
                {
                    tx = x >> i;
                    ty = y >> i;
                    tz = CORDIC_Atanh[i];
                    x += -y > 0 ? ty : -ty;
                    z -= -y > 0 ? tz : -tz;
                    y += -y > 0 ? tx : -tx;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long NormalizeRadian(long x)
        {
            for (var i = 0; i < FracBits - 3; i++)
                x %= PIRawMulN >> i;
            return x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static long NormalizeHyperbolic(long x, out int q)
        {
            q = 0;
            for (var i = 0; i <= 4; i++)
            {
                if (x > LN2RawMul16 >> i)
                {
                    x -= LN2RawMul16 >> i;
                    q |= 1 << (4 - i);
                }
            }

            return x;
        }

        private static void BuildCORDIC(out fp[] atan, out fp k, out fp[] atanh, out fp kh)
        {
            atan = new fp[FracBits + 1];
            atanh = new fp[FracBits + 1];
            for (var i = 0; i < atan.Length; i++)
            {
                var t = System.Math.Pow(2, -i);
                atan[i] = System.Math.Atan(t);
                atanh[i] = 0.5 * System.Math.Log((1 + t * 0.5) / (1 - t * 0.5));
            }

            var x = IntOne;
            var y = 0L;
            var z = 0L;
            CORDICCircular(ref z, ref x, ref y);
            k = 1 / new fp(x);

            x = IntOne;
            y = 0L;
            z = 0L;
            CORDICHyperbolic(ref z, ref x, ref y);
            kh = 1 / new fp(x);
        }
    }
}