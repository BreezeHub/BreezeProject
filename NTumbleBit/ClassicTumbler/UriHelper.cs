using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace NBitcoin.Payment
{
	static class UriHelper
	{
		public static Dictionary<string, string> DecodeQueryParameters(string uri)
		{
			if(uri == null)
				throw new ArgumentNullException("uri");

			if(uri.Length == 0)
				return new Dictionary<string, string>();

			return uri
					.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
					.Select(kvp => kvp.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries))
					.ToDictionary(kvp => kvp[0],
									kvp => kvp.Length > 2 ?
										UrlDecode(string.Join("=", kvp, 1, kvp.Length - 1)) :
									(kvp.Length > 1 ? UrlDecode(kvp[1]) : ""));
		}
		
		public static string UrlDecode(string str)
        {
            return UrlDecode(str, Encoding.UTF8);
        }

        static void WriteCharBytes(IList buf, char ch, Encoding e)
        {
            if(ch > 255)
            {
                foreach(byte b in e.GetBytes(new char[] { ch }))
                    buf.Add(b);
            }
            else
                buf.Add((byte)ch);
        }

        public static string UrlDecode(string s, Encoding e)
        {
            if(null == s)
                return null;

            if(s.IndexOf('%') == -1 && s.IndexOf('+') == -1)
                return s;

            if(e == null)
                e = Encoding.UTF8;

            long len = s.Length;
            var bytes = new List<byte>();
            int xchar;
            char ch;

            for(int i = 0; i < len; i++)
            {
                ch = s[i];
                if(ch == '%' && i + 2 < len && s[i + 1] != '%')
                {
                    if(s[i + 1] == 'u' && i + 5 < len)
                    {
                        // unicode hex sequence
                        xchar = GetChar(s, i + 2, 4);
                        if(xchar != -1)
                        {
                            WriteCharBytes(bytes, (char)xchar, e);
                            i += 5;
                        }
                        else
                            WriteCharBytes(bytes, '%', e);
                    }
                    else if((xchar = GetChar(s, i + 1, 2)) != -1)
                    {
                        WriteCharBytes(bytes, (char)xchar, e);
                        i += 2;
                    }
                    else
                    {
                        WriteCharBytes(bytes, '%', e);
                    }
                    continue;
                }

                if(ch == '+')
                    WriteCharBytes(bytes, ' ', e);
                else
                    WriteCharBytes(bytes, ch, e);
            }

            byte[] buf = bytes.ToArray();
            bytes = null;
            return e.GetString(buf, 0, buf.Length);
        }
	    
	    static int GetInt(byte b)
	    {
	        char c = (char)b;
	        if(c >= '0' && c <= '9')
	            return c - '0';

	        if(c >= 'a' && c <= 'f')
	            return c - 'a' + 10;

	        if(c >= 'A' && c <= 'F')
	            return c - 'A' + 10;

	        return -1;
	    }

	    static int GetChar(string str, int offset, int length)
	    {
	        int val = 0;
	        int end = length + offset;
	        for(int i = offset; i < end; i++)
	        {
	            char c = str[i];
	            if(c > 127)
	                return -1;

	            int current = GetInt((byte)c);
	            if(current == -1)
	                return -1;
	            val = (val << 4) + current;
	        }

	        return val;
	    }
	}
}
