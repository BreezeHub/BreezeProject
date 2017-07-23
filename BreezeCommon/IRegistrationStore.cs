using System;
using System.Collections.Generic;

namespace BreezeCommon
{
    public interface IRegistrationStore
    {
	    string Name { get; }

        bool Add(RegistrationRecord tx);
        List<RegistrationRecord> GetByServerId(string serverId);
        List<RegistrationRecord> GetAll();
    }
}