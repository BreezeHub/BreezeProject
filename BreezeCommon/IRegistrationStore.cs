using System;
using System.Collections.Generic;

namespace BreezeCommon
{
    public interface IRegistrationStore
    {
	    string Name { get; }

        bool Add(RegistrationRecord record);
        List<RegistrationRecord> GetByServerId(string serverId);
		List<RegistrationRecord> GetAll();
		RegistrationRecord GetByGuid(Guid guid);
        bool Delete(Guid guid);
        bool Delete(RegistrationRecord record);
    }
}