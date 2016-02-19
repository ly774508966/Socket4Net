﻿using System;
using System.Collections.Generic;

namespace socket4net
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class ComponentIdAttribute : Attribute
    {
        public short Id { get; private set; }
        public ComponentIdAttribute(short id)
        {
            Id = id;
        }
    }

    public class ComponentIdCache : AttributesCache<short, ComponentIdAttribute>
    {
        private static ComponentIdCache _ins;
        public static ComponentIdCache Ins
        {
            get { return _ins ?? (_ins = new ComponentIdCache()); }
        }

        protected override short Handle(IEnumerable<ComponentIdAttribute> attributes)
        {
            throw new NotImplementedException("");
        }

        protected override short Handle(ComponentIdAttribute attribute)
        {
            return attribute.Id;
        }
    }
}