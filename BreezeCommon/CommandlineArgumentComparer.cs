using System.Collections.Generic;

namespace BreezeCommon
{
	public class CommandlineArgumentComparer : IEqualityComparer<string>
	{
		/// <summary>
		/// Compares command line switch (optionally prefixed with dash or two dashes) to the argument name
		/// </summary>
		/// <param name="commandLineSwitch">Switch option passed to from the comman line</param>
		/// <param name="argumentName">Argument name without any dashes</param>
		/// <returns>Returns true if argumentName matches commandLineSwitch</returns>
		public bool Equals(string commandLineSwitch, string argumentName)
		{
			commandLineSwitch = commandLineSwitch.ToLower();
			argumentName = argumentName.ToLower();

			return (commandLineSwitch.Equals(argumentName) || commandLineSwitch.Equals($"-{argumentName}") || commandLineSwitch.Equals($"--{argumentName}"));
		}

		public int GetHashCode(string obj)
		{
			return obj.GetHashCode();
		}
	}
}
