using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using NTumbleBit.ClassicTumbler;
using NTumbleBit.ClassicTumbler.Client;

namespace NTumbleBit
{
	public class CycleProgressInfo
	{
		public CyclePeriod Period { get; private set; }
		public int Height { get; private set; }
		public int BlocksLeft { get; private set; }
		public int Start { get; private set; }
		public bool Failed { get; private set; } = false;

		[JsonIgnore]
		public PaymentStateMachineStatus StatusEnum { get; private set; }

		[JsonIgnore]
		public CyclePhase PhaseEnum { get; private set; }

		public string AsciiArt { get; private set; }

		public string Status
		{
			get { return this.StatusEnum.ToString(); }
		}

		public string Phase {
			get { return this.PhaseEnum.ToString(); }
		}



		public CycleProgressInfo(CyclePeriod period, int height, int blocksLeft, int start, PaymentStateMachineStatus status, CyclePhase phase, string asciiArt)
		{
			this.Period = period;
			this.Height = height;
			this.BlocksLeft = blocksLeft;
			this.Start = start;
			this.StatusEnum = status;
			this.PhaseEnum = phase;
			this.AsciiArt = asciiArt;

		    this.CheckForFailedState();
		}

	    private void CheckForFailedState()
	    {
	        if (this.PhaseEnum > CyclePhase.TumblerChannelEstablishment &&
	            this.StatusEnum == PaymentStateMachineStatus.Registered)
	            this.Failed = true;
	    }
    }

	public class ProgressInfo
    {
		public int Height { get; private set; }

		public List<CycleProgressInfo> CycleProgressInfoList = new List<CycleProgressInfo>();

	    public ProgressInfo(int height)
	    {
		    this.Height = height;
	    }

	    public void Save()
	    {
	        string folder;
	        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
	            folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StratisNode\\bitcoin\\TumbleBit");
	        else
	            folder = Path.Combine(Environment.GetEnvironmentVariable("HOME"), ".stratisnode", "bitcoin", "TumbleBit");

		    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
		    string filename = Path.Combine(folder, "tb_progress.json");

		    using (var file = File.CreateText(filename))
			{
				JsonSerializer serializer = new JsonSerializer();
			    serializer.Serialize(file, this);
			}
		}

	    public static void RemoveProgressFile()
	    {
			string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "breeze-ui");
		    string filename = Path.Combine(folder, "tb_progress.json");
			if (File.Exists(filename)) File.Delete(filename);
		}
	}
}