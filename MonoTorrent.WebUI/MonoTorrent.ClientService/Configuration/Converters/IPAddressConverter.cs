using System;
using System.Globalization;
using System.ComponentModel;
using System.Net;

namespace MonoTorrent.ClientService.Configuration.Converters
{
    internal class IPAddressConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return typeof(String).IsAssignableFrom(sourceType)
                || typeof(long).IsAssignableFrom(sourceType)
                || typeof(byte[]).IsAssignableFrom(sourceType)
                || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType.IsAssignableFrom(typeof(IPAddress))
                || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string)
                return IPAddress.Parse((string)value);
            else if (value is long)
                return new IPAddress((long)value);
            else if (value is byte[])
                return new IPAddress((byte[])value);
            else
                return base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            IPAddress addr = value as IPAddress;

            if (addr == null)
                return base.ConvertTo(context, culture, value, destinationType);
            else if (destinationType.IsAssignableFrom(typeof(String)))
                return addr.ToString();
            else if (destinationType.IsAssignableFrom(typeof(byte[])))
                return addr.GetAddressBytes();
            else
                return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
