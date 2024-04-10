using ServiceStack;

namespace Quote_To_Deal.Extensions
{
    public static class TransformExtension
    {
        public static T ConvertTo<T>(this object source)
            => source.ConvertTo<T>(false);
    }
}
