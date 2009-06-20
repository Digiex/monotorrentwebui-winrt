using System;
using System.Text;
using System.Globalization;
using System.ComponentModel;
using MonoTorrent.Client.Encryption;

namespace MonoTorrent.ClientService
{
	internal class EncryptionTypesConverter : TypeConverter
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
                return Enum.Parse(typeof(EncryptionTypes), str);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(String))
                return ((EncryptionTypes)value).ToString();
            else
                return base.ConvertTo(context, culture, value, destinationType);
        }
	}
}
