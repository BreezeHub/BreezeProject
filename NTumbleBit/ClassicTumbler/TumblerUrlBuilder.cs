using NBitcoin;
using NBitcoin.Payment;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using NBitcoin.DataEncoders;

namespace NTumbleBit.ClassicTumbler
{
	public class TumblerUrlBuilder
	{
		public TumblerUrlBuilder()
		{

		}
		public TumblerUrlBuilder(Uri uri)
			: this(uri.AbsoluteUri)
		{
			if(uri == null)
				throw new ArgumentNullException("uri");
		}

		public TumblerUrlBuilder(string uri)
		{
			if(uri == null)
				throw new ArgumentNullException("uri");
			if(!uri.StartsWith("ctb:", StringComparison.OrdinalIgnoreCase))
				throw new FormatException("Invalid scheme");
			uri = uri.Remove(0, "ctb:".Length);
			if(uri.StartsWith("//"))
				uri = uri.Remove(0, 2);

			var paramStart = uri.IndexOf('?');
			string address = null;
			if(paramStart == -1)
				address = uri;
			else
			{
				address = uri.Substring(0, paramStart);
			}
			if(address != String.Empty)
			{
				if(address.EndsWith("/", StringComparison.OrdinalIgnoreCase))
					address = address.Substring(0, address.Length - 1);
				Port = 80;
				var split = address.Split(':');
				if(split.Length != 1 && split.Length != 2)
					throw new FormatException("Invalid host");

				if(split.Length == 2)
					Port = int.Parse(split[1]);
				var host = split[0];
				Host = host;
			}
			uri = uri.Remove(0, paramStart + 1);  //+1 to move past '?'

			Dictionary<string, string> parameters;
			try
			{
				parameters = UriHelper.DecodeQueryParameters(uri);
			}
			catch(ArgumentException)
			{
				throw new FormatException("A URI parameter is duplicated");
			}

			if(parameters.ContainsKey("h"))
			{
				ConfigurationHash = new uint160(parameters["h"]);
				parameters.Remove("h");
			}
			else
			{
				throw new FormatException("The configuration hash is missing");
			}

			//var reqParam = parameters.Keys.FirstOrDefault(k => k.StartsWith("req-", StringComparison.OrdinalIgnoreCase));
			//if(reqParam != null)
			//	throw new FormatException("Non compatible required parameter " + reqParam);
		}

		string _Host;
		public string Host
		{
			get
			{
				return _Host;
			}
			set
			{
				if(value != null)
				{
					if(!value.EndsWith(".onion", StringComparison.Ordinal) &&
					   !value.EndsWith(".dummy", StringComparison.Ordinal) &&
					   !IsIp(value))
						throw new FormatException("Host can only be an onion address, dummy regtest address or an IP");

				}
				_Host = value;
			}
		}

		private bool IsIp(string value)
		{
			try
			{
				IPAddress.Parse(value);
				return true;
			}
			catch { return false; }
		}

		public int Port
		{
			get;
			set;
		}

		public uint160 ConfigurationHash
		{
			get; set;
		}

		public Uri GetRoutableUri(bool includeConfigurationHash)
		{
			UriBuilder builder = new UriBuilder();
			builder.Scheme = "http";
			builder.Host = Host;
			if(builder.Port != 80)
				builder.Port = Port;
			if(includeConfigurationHash)
			{
				builder.Path = "api/v1/tumblers/" + ConfigurationHash;
			}
			return builder.Uri;
		}

		public bool IsOnion
		{
			get
			{
				return Host.EndsWith(".onion", StringComparison.Ordinal);
			}
		}

		private static void WriteParameters(Dictionary<string, string> parameters, StringBuilder builder)
		{
			bool first = true;
			foreach(var parameter in parameters)
			{
				if(first)
				{
					first = false;
					builder.Append("?");
				}
				else
					builder.Append("&");
				builder.Append(parameter.Key);
				builder.Append("=");
				builder.Append(UrlEncode(parameter.Value));
			}
		}

		public override string ToString()
		{
			Dictionary<string, string> parameters = new Dictionary<string, string>();
			StringBuilder builder = new StringBuilder();
			builder.Append("ctb://");
			if(Host != null)
			{
				builder.Append(Host.ToString());
			}
			if(Port != 80)
			{
				builder.Append(":" + Port.ToString());
			}
			if(ConfigurationHash != null)
			{
				parameters.Add("h", ConfigurationHash.ToString());
			}

			WriteParameters(parameters, builder);
			return builder.ToString();
		}
		
		public static string UrlEncode(string str)		
		{		
			return UrlEncode(str, Encoding.UTF8);		
		}		
 
		public static string UrlEncode(string s, Encoding Enc)		
		{		
			if(s == null)		
				return null;		
 		
			if(s == String.Empty)		
				return String.Empty;		
 		
			bool needEncode = false;		
			int len = s.Length;		
			for(int i = 0; i < len; i++)		
			{		
				char c = s[i];		
				if((c < '0') || (c < 'A' && c > '9') || (c > 'Z' && c < 'a') || (c > 'z'))		
				{		
					if(HttpEncoderNotEncoded(c))
						continue;
 		
					needEncode = true;		
					break;		
				}		
			}		
 		
			if(!needEncode)		
				return s;		
 		
			// avoided GetByteCount call		
			byte[] bytes = new byte[Enc.GetMaxByteCount(s.Length)];		
			int realLen = Enc.GetBytes(s, 0, s.Length, bytes, 0);		
			return Encoders.ASCII.EncodeData(UrlEncodeToBytes(bytes, 0, realLen));		
		}		
		
		public static byte[] UrlEncodeToBytes(byte[] bytes, int offset, int count)		
		{		
			if(bytes == null)		
				return null;		
			#if NET_4_0		
				return HttpEncoder.Current.UrlEncode (bytes, offset, count);		
			#else		
				return HttpEncoderUrlEncodeToBytes(bytes, offset, count);		
			#endif		
		}
		
		private static bool HttpEncoderNotEncoded(char c)
		{
			//query strings are allowed to contain both ? and / characters, see section 3.4 of http://www.ietf.org/rfc/rfc3986.txt, which is basically the spec written by Tim Berners-Lee and friends governing how the web should operate.
			//pchar         = unreserved / pct-encoded / sub-delims / ":" / "@"
			//query         = *( pchar / "/" / "?" )
			return (c == '!' || c == '(' || c == ')' || c == '*' || c == '-' || c == '.' || c == '_' || c == '?' || c == '/' || c == ':'
			#if !NET_4_0
			        || c == '\''
			#endif
			);
		}
		
		private static byte[] HttpEncoderUrlEncodeToBytes(byte[] bytes, int offset, int count)
		{
			if(bytes == null)
				throw new ArgumentNullException("bytes");

			int blen = bytes.Length;
			if(blen == 0)
				return new byte[0];

			if(offset < 0 || offset >= blen)
				throw new ArgumentOutOfRangeException("offset");

			if(count < 0 || count > blen - offset)
				throw new ArgumentOutOfRangeException("count");

			MemoryStream result = new MemoryStream(count);
			int end = offset + count;
			for(int i = offset; i < end; i++)
				HttpEncoderUrlEncodeChar((char)bytes[i], result, false);

			return result.ToArray();
		}
		
		private static void HttpEncoderUrlEncodeChar(char c, Stream result, bool isUnicode)
		{
			char[] hexChars = "0123456789abcdef".ToCharArray();
			
			if(c > 255)
			{
				//FIXME: what happens when there is an internal error?
				//if (!isUnicode)
				//    throw new ArgumentOutOfRangeException ("c", c, "c must be less than 256");
				int idx;
				int i = (int)c;

				result.WriteByte((byte)'%');
				result.WriteByte((byte)'u');
				idx = i >> 12;
				result.WriteByte((byte)hexChars[idx]);
				idx = (i >> 8) & 0x0F;
				result.WriteByte((byte)hexChars[idx]);
				idx = (i >> 4) & 0x0F;
				result.WriteByte((byte)hexChars[idx]);
				idx = i & 0x0F;
				result.WriteByte((byte)hexChars[idx]);
				return;
			}

			if(c > ' ' && HttpEncoderNotEncoded(c))
			{
				result.WriteByte((byte)c);
				return;
			}
			if((c < '0') ||
			   (c < 'A' && c > '9') ||
			   (c > 'Z' && c < 'a') ||
			   (c > 'z'))
			{
				if(isUnicode && c > 127)
				{
					result.WriteByte((byte)'%');
					result.WriteByte((byte)'u');
					result.WriteByte((byte)'0');
					result.WriteByte((byte)'0');
				}
				else
					result.WriteByte((byte)'%');

				int idx = ((int)c) >> 4;
				result.WriteByte((byte)hexChars[idx]);
				idx = ((int)c) & 0x0F;
				result.WriteByte((byte)hexChars[idx]);
			}
			else
				result.WriteByte((byte)c);
		}
	}
}
