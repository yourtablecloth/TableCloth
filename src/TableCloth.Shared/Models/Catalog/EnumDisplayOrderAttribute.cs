using System;

namespace TableCloth.Models.Catalog
{
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class EnumDisplayOrderAttribute : Attribute
    {
        public EnumDisplayOrderAttribute(int order)
        {
            this._order = order;
        }

        private readonly int _order;

        public int Order => _order;

        public override string ToString() => $"{{ Display Order: {_order} }}";
    }
}
