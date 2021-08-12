using System;

namespace Raptor.Ucommerce.Exceptions
{
    public class ObjectNotFoundException : Exception
    {
        public ObjectNotFoundException(object identifier, Type clazz)
            : base($"No object with the given identifier {identifier} exists for type {clazz}")
        {
        }
    }
}
