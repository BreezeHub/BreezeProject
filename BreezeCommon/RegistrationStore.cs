using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Newtonsoft.Json;
using System.Runtime.InteropServices;
using NBitcoin;
using NBitcoin.Protocol;

namespace BreezeCommon
{
	public class RegistrationStore : IRegistrationStore
	{
		private string StorePath;
		private static object lock_object = new object();

        public RegistrationStore()
        {
        }

		public RegistrationStore(string storePath)
		{
			StorePath = storePath;
            // TODO: Get the parent folder of the file path
            //Directory.CreateDirectory(StorePath);
		}

        public void SetStorePath(string storePath)
        {
            // May not be able to inject the necessary path information directly,
            // so this is a helper function to set the path independently after
            // instantiation
            Directory.CreateDirectory(storePath);
            StorePath = Path.Combine(storePath, StoreFileName);
        }

		public string Name { get; } = "RegistrationStore";
		public string StoreFileName { get; } = "registrationHistory.json";

		public bool Add(RegistrationRecord regRecord)
		{
			lock (RegistrationStore.lock_object)
			{
				List<RegistrationRecord> registrations = GetRecordsOrCreateFile();

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
		}

        /// <summary>
        /// Checks if the server ID in the supplied record already exists in the store,
        /// and removes all other records before adding.
        /// </summary>
        /// <param name="regRecord"></param>
        /// <returns></returns>
        public bool AddWithReplace(RegistrationRecord regRecord)
        {
            foreach (RegistrationRecord record in GetByServerId(regRecord.Record.ServerId))
            {
                Delete(record.RecordGuid);
            }

            lock (RegistrationStore.lock_object)
            {
                List<RegistrationRecord> registrations = GetRecordsOrCreateFile();

                registrations.Add(regRecord);

                string regJson = JsonConvert.SerializeObject(registrations);
                File.WriteAllText(StorePath, regJson);

                return true;
            }
        }

        public void AddCapsule(RegistrationCapsule capsule, Network network)
		{
			RegistrationToken token = new RegistrationToken();
			token.ParseTransaction(capsule.RegistrationTransaction, network);
			RegistrationRecord record = new RegistrationRecord(DateTime.Now,
				Guid.NewGuid(),
				capsule.RegistrationTransaction.GetHash().ToString(),
				capsule.RegistrationTransaction.ToHex(),
				token,
				capsule.RegistrationTransactionProof);

			Add(record);
		}

		public List<RegistrationRecord> GetByServerId(string serverId)
		{
			List<RegistrationRecord> registrations = GetRecordsOrCreateFile();
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
			return GetRecordsOrCreateFile();
		}

		public List<RegistrationCapsule> GetAllAsCapsules()
		{
			List<RegistrationCapsule> capsuleList = new List<RegistrationCapsule>();

			foreach (RegistrationRecord record in GetRecordsOrCreateFile())
			{
				RegistrationCapsule tempCapsule =
					new RegistrationCapsule(record.RecordTxProof, Transaction.Parse(record.RecordTxHex));
				capsuleList.Add(tempCapsule);
			}

			return capsuleList;
		}

		public RegistrationRecord GetByGuid(Guid guid)
		{
			List<RegistrationRecord> registrations = GetRecordsOrCreateFile();

			foreach (RegistrationRecord record in registrations)
			{
				if (record.RecordGuid == guid)
				{
					return record;
				}
			}

			return null;
		}

		public bool Delete(Guid guid)
		{
			lock (RegistrationStore.lock_object)
			{
				List<RegistrationRecord> registrations = GetRecordsOrCreateFile();
				List<RegistrationRecord> modified = new List<RegistrationRecord>();

				foreach (RegistrationRecord record in registrations)
				{
					if (record.RecordGuid != guid)
						modified.Add(record);
				}

				try
				{
					string regJson = JsonConvert.SerializeObject(modified);
					File.WriteAllText(StorePath, regJson);
				}
				catch (IOException)
				{
					return false;
				}
				return true;
			}
		}

		public bool Delete(RegistrationRecord record)
		{
			try
			{
				Delete(record.RecordGuid);
			}
			catch (Exception)
			{
				return false;
			}

			return true;
		}

        public bool DeleteAllForServer(string serverId)
        {
            try
            {
                foreach (RegistrationRecord record in this.GetByServerId(serverId))
                {
                    this.Delete(record.RecordGuid);
                }
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        private List<RegistrationRecord> GetRecordsOrCreateFile()
		{
			lock (RegistrationStore.lock_object)
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
}
