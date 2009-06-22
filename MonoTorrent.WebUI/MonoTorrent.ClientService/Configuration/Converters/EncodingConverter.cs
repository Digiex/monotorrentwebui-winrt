using System;
using System.Text;
using System.Globalization;
using System.ComponentModel;

namespace MonoTorrent.ClientService.Configuration.Converters
{
    /// <summary>
    /// Converts a string containing an Encoding's WebName to System.Text.Encoding, and back.
    /// </summary>
    internal class EncodingConverter : TypeConverter
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
                return Encoding.GetEncoding(str);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(String))
                return ((Encoding)value).WebName;
            else
                return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
