using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Microsoft.Azure.Documents
{
    internal class PropertyInfoOverrideTypeWrapper : PropertyInfo
    {
        private PropertyInfo wrapped;
        private Type overrideType;

        public PropertyInfoOverrideTypeWrapper(PropertyInfo wrapped, Type overrideType)
        {
            this.wrapped = wrapped;
            this.overrideType = overrideType;
        }

        public override Type PropertyType
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

        public override object[] GetCustomAttributes(bool inherit)
        {
            return wrapped.GetCustomAttributes(inherit);
        }

        public override IList<CustomAttributeData> GetCustomAttributesData()
        {
            return wrapped.GetCustomAttributesData();
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return wrapped.GetCustomAttributes(attributeType, inherit);
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

        public override object GetValue(object obj, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            return wrapped.GetValue(obj, invokeAttr, binder, index, culture);
        }

        public override bool CanWrite
        {
            get
            {
                return wrapped.CanWrite;
            }
        }

        public override bool CanRead
        {
            get
            {
                return wrapped.CanRead;
            }
        }

        public override PropertyAttributes Attributes
        {
            get
            {
                return wrapped.Attributes;
            }
        }

        public override ParameterInfo[] GetIndexParameters()
        {
            return wrapped.GetIndexParameters();
        }

        public override MethodInfo GetSetMethod(bool nonPublic)
        {
            return wrapped.GetSetMethod(nonPublic);
        }

        public override MethodInfo GetGetMethod(bool nonPublic)
        {
            return wrapped.GetGetMethod(nonPublic);
        }

        public override MethodInfo[] GetAccessors(bool nonPublic)
        {
            return wrapped.GetAccessors(nonPublic);
        }

        public override void SetValue(object obj, object value, BindingFlags invokeAttr, Binder binder, object[] index, CultureInfo culture)
        {
            wrapped.SetValue(obj, value, invokeAttr, binder, index, culture);
        }
    }
}
