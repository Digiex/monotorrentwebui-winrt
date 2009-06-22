using System;
using System.IO;
using System.Text;
using System.Globalization;
using System.ComponentModel;

namespace MonoTorrent.ClientService.Configuration.Converters
{
    /// <summary>
    /// Converts a string into a System.IO.DirectoryInfo object and vice-versa.
    /// </summary>
    internal class DirectoryInfoConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return (sourceType == typeof(String));
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return (destinationType == typeof(String));
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string str = value as string;

            if (str == null)
                return base.ConvertFrom(context, culture, value);
            else
                return new DirectoryInfo(str);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(String))
                return ((DirectoryInfo)value).ToString();
            else
                return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
