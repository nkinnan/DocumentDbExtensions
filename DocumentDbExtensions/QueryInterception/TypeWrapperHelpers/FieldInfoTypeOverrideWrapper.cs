using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Microsoft.Azure.Documents
{
    internal class FieldInfoOverrideTypeWrapper : FieldInfo
    {
        private FieldInfo wrapped;
        private Type overrideType;

        public FieldInfoOverrideTypeWrapper(FieldInfo wrapped, Type overrideType)
        {
            this.wrapped = wrapped;
            this.overrideType = overrideType;
        }

        public override Type FieldType
        {
            get
            {
                return overrideType;
            }
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return wrapped.IsDefined(attributeType, inherit);
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return wrapped.GetCustomAttributes(attributeType, inherit);
        }
        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return wrapped.GetCustomAttributesData();
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return wrapped.GetCustomAttributes(inherit);
        }

        public override Type ReflectedType
        {
            get
            {
                return wrapped.ReflectedType;
            }
        }

        public override Type DeclaringType
        {
            get
            {
                return wrapped.DeclaringType;
            }
        }

        public override string Name
        {
            get
            {
                return wrapped.Name;
            }
        }

        public override FieldAttributes Attributes
        {
            get
            {
                return wrapped.Attributes;
            }
        }

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, CultureInfo culture)
        {
            wrapped.SetValue(obj, value, invokeAttr, binder, culture);
        }

        public override object GetValue(object obj)
        {
            return wrapped.GetValue(obj);
        }

        public override RuntimeFieldHandle FieldHandle
        {
            get
            {
                return wrapped.FieldHandle;
            }
        }
    }
}
