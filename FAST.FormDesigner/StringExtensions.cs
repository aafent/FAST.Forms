namespace FAST.FormDesigner
{
    public static class StringExtensions
    {
        public static string OrDefault(this string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
