using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Newtonsoft.Json;

namespace BreezeCommon
{
    public class RegistrationStore : IRegistrationStore
    {
        string StorePath;

        public RegistrationStore(string storePath)
        {
            StorePath = storePath;
        }

	    public string Name { get; } = "RegistrationStore";

        public bool Add(RegistrationRecord regRecord)
        {
            List<RegistrationRecord> registrations = new List<RegistrationRecord>();

            try
            {
                registrations = JsonConvert.DeserializeObject<List<RegistrationRecord>>(File.ReadAllText(StorePath));

                // If file is empty the list will deserialise to null
                if (registrations == null)
                    registrations = new List<RegistrationRecord>();
            }
            catch (FileNotFoundException)
            {
                FileStream temp = File.Create(StorePath);
                temp.Dispose();
            }

            registrations.Add(regRecord);

			//JsonSerializerSettings settings = new JsonSerializerSettings();
			//settings.Converters.Add(new IPAddressConverter());
			//settings.Formatting = Formatting.Indented;

			//JsonSerializerSettings isoDateFormatSettings = new JsonSerializerSettings
            //{
            //    DateFormatHandling = DateFormatHandling.IsoDateFormat
            //};

            string regJson = JsonConvert.SerializeObject(registrations);
            File.WriteAllText(StorePath, regJson);

            return true;
        }

		public List<RegistrationRecord> GetByServerId(string serverId)
        {
            List<RegistrationRecord> registrations = new List<RegistrationRecord>();

            try
            {
                registrations = JsonConvert.DeserializeObject<List<RegistrationRecord>>(File.ReadAllText(StorePath));

				// If file is empty the list will deserialise to null
				if (registrations == null)
					registrations = new List<RegistrationRecord>();
            }
            catch (FileNotFoundException)
            {
				FileStream temp = File.Create(StorePath);
				temp.Dispose();
            }

            List<RegistrationRecord> filtered = new List<RegistrationRecord>();

            foreach (RegistrationRecord record in registrations)
            {
                if (record.Record.ServerId == serverId)
                {
                    filtered.Add(record);
                }
            }

            return filtered;
        }

		public List<RegistrationRecord> GetAll()
        {
			List<RegistrationRecord> registrations = new List<RegistrationRecord>();

			try
			{
				registrations = JsonConvert.DeserializeObject<List<RegistrationRecord>>(File.ReadAllText(StorePath));

				// If file is empty the list will deserialise to null
				if (registrations == null)
					registrations = new List<RegistrationRecord>();
			}
			catch (FileNotFoundException)
			{
				FileStream temp = File.Create(StorePath);
				temp.Dispose();
			}

			return registrations;
        }
    }
}
