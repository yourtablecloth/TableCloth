using System;

namespace TableCloth.Models.Catalog
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EnumDisplayOrderAttribute : Attribute
    {
        public int Order { get; set; }

        public override string ToString() => $"{{ Display Order: {Order} }}";
    }
}
