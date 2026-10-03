using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace GameLauncher.Ranking
{
    /// <summary>1024桁の有効数字と±10000の10進指数を持つ正確なスコアです。</summary>
    public readonly struct RankingScore : IComparable<RankingScore>, IEquatable<RankingScore>
    {
        private readonly string digits;
        private readonly int scale;
        private readonly bool negative;
        private string Digits => digits ?? "0";
        private int Exponent => scale + Digits.Length - 1;
        private RankingScore(string digits, int scale, bool negative)
        { this.digits = digits; this.scale = scale; this.negative = negative; }

        /// <summary>丸めていない、通信・保存用の数値文字列です。</summary>
        public string RawValue => Digits == "0" ? "0" :
            (negative ? "-" : "") + Digits[0] + (Digits.Length > 1 ? "." + Digits.Substring(1) : "") + "e" + Exponent.ToString(CultureInfo.InvariantCulture);

        public static RankingScore Parse(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            value = value.Trim();
            if (value.Length == 0 || value.Length > 21032) throw new ArgumentException("スコアは有効数字1024桁以内です。", nameof(value));
            var match = Regex.Match(value, @"\A([+-]?)([0-9]*)(?:\.([0-9]*))?(?:[eE]([+-]?[0-9]+))?\z");
            if (!match.Success || match.Groups[2].Length + match.Groups[3].Length == 0)
                throw new ArgumentException("有限な数値を指定してください。NaN・Infinityは使えません。", nameof(value));
            int exponent = 0;
            if (match.Groups[4].Success && (!int.TryParse(match.Groups[4].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent) || exponent < -10000 || exponent > 10000))
                throw new ArgumentOutOfRangeException(nameof(value), "10進指数は-10000〜10000です。");
            string digits = (match.Groups[2].Value + match.Groups[3].Value).TrimStart('0');
            if (digits.Length == 0) return default;
            int scale = exponent - match.Groups[3].Length;
            int trimmed = digits.TrimEnd('0').Length;
            scale += digits.Length - trimmed; digits = digits.Substring(0, trimmed);
            int magnitude = scale + digits.Length - 1;
            if (digits.Length > 1024 || magnitude < -10000 || magnitude > 10000)
                throw new ArgumentOutOfRangeException(nameof(value), "有効数字1024桁以内、10進指数-10000〜10000です。");
            return new RankingScore(digits, scale, match.Groups[1].Value == "-");
        }

        public int CompareTo(RankingScore other)
        {
            if (negative != other.negative) return negative ? -1 : 1;
            int comparison;
            if (Digits == "0" || other.Digits == "0") comparison = Digits == other.Digits ? 0 : Digits == "0" ? -1 : 1;
            else
            {
                comparison = Exponent.CompareTo(other.Exponent);
                if (comparison == 0)
                {
                    int size = Math.Max(Digits.Length, other.Digits.Length);
                    comparison = string.CompareOrdinal(Digits.PadRight(size, '0'), other.Digits.PadRight(size, '0'));
                }
            }
            return negative ? -comparison : comparison;
        }

        public bool Equals(RankingScore other) => CompareTo(other) == 0;
        public override bool Equals(object other) => other is RankingScore score && Equals(score);
        public override int GetHashCode() => RawValue.GetHashCode();

        /// <summary>大きな値・小さな値を4有効桁の指数表記にする表示用文字列です。</summary>
        public override string ToString()
        {
            if (Digits == "0") return "0";
            string sign = negative ? "-" : "";
            if (Exponent >= 9 || Exponent <= -5)
            {
                string head = Digits.Substring(0, Math.Min(4, Digits.Length)).PadRight(4, '0');
                if (Digits.Length > 4 && Digits[4] >= '5') head = (int.Parse(head, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
                int exponent = Exponent;
                if (head.Length > 4) { head = "1000"; exponent++; }
                string fraction = head.Substring(1).TrimEnd('0');
                return sign + head[0] + (fraction.Length > 0 ? "." + fraction : "") + "e" + exponent.ToString(CultureInfo.InvariantCulture);
            }
            int point = Exponent + 1;
            string plain = point <= 0 ? "0." + new string('0', -point) + Digits :
                point >= Digits.Length ? Digits + new string('0', point - Digits.Length) : Digits.Insert(point, ".");
            return sign + plain;
        }

        public BigInteger ToBigInteger()
        {
            if (scale < 0) throw new InvalidOperationException("小数スコアは整数に正確に変換できません。");
            BigInteger value = BigInteger.Parse(Digits, CultureInfo.InvariantCulture) * BigInteger.Pow(10, scale);
            return negative ? -value : value;
        }
        public double ToDouble()
        {
            if (!double.TryParse(RawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || double.IsInfinity(value) || double.IsNaN(value) || (value == 0 && Digits != "0"))
                throw new OverflowException("このスコアはdoubleの範囲外です。RawValueを使ってください。");
            return value;
        }

        public static implicit operator RankingScore(long value) => Parse(value.ToString(CultureInfo.InvariantCulture));
        public static implicit operator RankingScore(BigInteger value) => Parse(value.ToString(CultureInfo.InvariantCulture));
        public static implicit operator RankingScore(decimal value) => Parse(value.ToString(CultureInfo.InvariantCulture));
        public static implicit operator RankingScore(double value) => Parse(value.ToString("R", CultureInfo.InvariantCulture));
        public static bool operator ==(RankingScore a, RankingScore b) => a.Equals(b);
        public static bool operator !=(RankingScore a, RankingScore b) => !a.Equals(b);
        public static bool operator >(RankingScore a, RankingScore b) => a.CompareTo(b) > 0;
        public static bool operator <(RankingScore a, RankingScore b) => a.CompareTo(b) < 0;
        public static bool operator >=(RankingScore a, RankingScore b) => a.CompareTo(b) >= 0;
        public static bool operator <=(RankingScore a, RankingScore b) => a.CompareTo(b) <= 0;
    }
}
