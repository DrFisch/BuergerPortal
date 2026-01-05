using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace BuergerPortal.Api.Extensions
{
    public static class EnumExtensions
    {
        public static string GetDisplayName(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attr = field?.GetCustomAttribute<DisplayAttribute>();
            return attr?.GetName() ?? value.ToString();
        }
    }
}
