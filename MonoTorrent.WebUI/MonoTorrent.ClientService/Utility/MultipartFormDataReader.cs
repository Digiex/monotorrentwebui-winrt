using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace MonoTorrent.ClientService
{
	public class MultipartFormDataReader
    {		
    	static readonly byte[] crlf = new byte[] { 0x0D, 0x0A }; // CRLF line terminator

    	public MultipartFormDataReader(System.Net.HttpListenerRequest request)
    		: this(request.ContentType, request.ContentEncoding, request.InputStream) { }
    	
    	public MultipartFormDataReader(string contentType, Encoding contentEncoding, Stream inputStream)
    		:this(contentType, contentEncoding, inputStream, 256) { }
    	
    	public MultipartFormDataReader(string contentType, Encoding contentEncoding, Stream inputStream, int bufferSize)
    	{
    		this.contentType = contentType;
    		this.contentEncoding = contentEncoding;
    		this.inputStream = inputStream;
    		
    		mode = ParsingMode.Headers;
    		
    		ParseContentType();
    		
    		mem = new MemoryStream(bufferSize);
    		text = new char[
	            contentEncoding.GetMaxCharCount(mem.Capacity)
	            ];
		}
		
		private void ParseContentType()
		{
			//boundary = Encoding.ASCII.GetBytes("---------------------------6085247902117339992135784599");
    		byte[] boundStr = Encoding.ASCII.GetBytes("--boundary####");
			
			boundary = new byte[boundStr.Length + crlf.Length];
			
			Array.Copy(crlf, boundary, crlf.Length);
			Array.Copy(boundStr, 0, boundary, crlf.Length, boundStr.Length);
			
//			char[] tokenDelims = new char[] { ';', '=' };
//			//Content-Type: multipart/mixed; boundary="gc0p4Jq0M2Yt08j34c0p"
//			int pos = 0;
//			
//			while(pos < contentType.Length)
//			{
//				int split = contentType.IndexOfAny('=', valuePos);
//				
//				string attrName = value.Substring(valuePos, split - valuePos).Trim();
//			}
		}
		
		public Encoding ContentEncoding
		{
			get { return contentEncoding; }
		}
		
		public Stream InputStream
		{
			get { return inputStream; }
		}
		
		private string contentType;
		private Encoding contentEncoding;
		private Stream inputStream;
		
		private byte[] boundary = null;
		
		private ParsingMode mode;
        /* preamble (ignore)
         * --boundary
         * <headers>
         * CRLF (empty line)
         * data
         * --boundary
         * <headers>
         * CRLF (empty line)
         * data
         * --boundary--
         * epilogue (ignore)
         */
        
        MemoryStream mem;
        char[] text;
        
        private void ResetMemStream()
        {
        	mem.Seek(0, SeekOrigin.Begin);
            mem.SetLength(0);
        }
        
       	public IDictionary<string, string> ReadHeaders()
		{
			IDictionary<string, string> headers = new Dictionary<string, string>();
			
			Decoder decoder = contentEncoding.GetDecoder();
                    	
        	byte[] buffer = new byte[
        		Math.Max(mem.Length, 32)
        		];
        	int count = mem.Read(buffer, 0, (int)mem.Length);
        	int crlfMatch = 0;
        	
        	do
        	{
        		Console.WriteLine("Read {0} bytes", count);
        		int bufferPos = 0;

        	    while(bufferPos < count)
        	    {
        	    	int startBufferPos = bufferPos;
        	    	int deltaMatch = crlfMatch;
        	    	
        	    	MatchByteSeq(buffer, ref bufferPos, count, crlf, ref crlfMatch);
        	    	
        	    	deltaMatch = Math.Max(0, (crlfMatch - deltaMatch));
        	    	int dataByteCount = (bufferPos - startBufferPos - deltaMatch);
        	    	
        	    	mem.Write(buffer, startBufferPos, dataByteCount);
        	    	
        	    	if(crlfMatch == crlf.Length) // line terminator
        	    	{
                        if(mem.Length == 0) // empty line
        	        	{
        	        		mode = ParsingMode.Data;
        	        		
        	        		ResetMemStream();
        	        		
                        	int unprocByteCount = Math.Max(0, (count - bufferPos - deltaMatch));
                        	Console.WriteLine("Empty line, terminating ReadHeaders. Bytes {0}+{1} unprocessed in buffer.", bufferPos, unprocByteCount);
                        	mem.Write(buffer, bufferPos, unprocByteCount);
                        	mem.Seek(0, SeekOrigin.Begin);
        	        		
        	        		return headers;
        	        	}
        	        	
        	        	byte[] textByteData = mem.GetBuffer();
        	        	
    	        		int lineChars = decoder.GetCharCount(textByteData, 0, (int)mem.Length);
    	        	
        	        	if(text.Length < lineChars) // make sure we have enough space
        	        		text = new char[lineChars];
	        	        
        	        	int textLength = decoder.GetChars(textByteData, 0, (int)mem.Length, text, 0);
        	        	
        	            // TODO: Parse line contents.
        	            //Content-Disposition: file; filename="file2.gif"
                        //Content-Type: image/gif
                        //Content-Transfer-Encoding: binary
                        
                        Console.WriteLine("Read line of text ({0} bytes counted):", mem.Length);
                        Console.Write('"');
                        Console.Write(text, 0, lineChars);
                        Console.WriteLine('"');
                        
        	        	if(!ParseHeaderLine(headers, text, textLength))
        	        	{
        	 				Console.WriteLine("Unable to parse header.");
                        }
                        
                        ResetMemStream();
                        
                        crlfMatch = 0;
    	            }
                }                
                Console.ReadKey(true);
            }
            while((count = inputStream.Read(buffer, 0, buffer.Length)) != 0);
            
            mode = ParsingMode.EndOfStream;
            
            return headers;
		}
		
		public void CopyData(Stream outputStream)
        {
        	if(mode != ParsingMode.Data)
        		throw new InvalidOperationException(
        			"MultipartFormReader is not at a data segment."
        			);
        	
        	byte[] buffer = mem.GetBuffer();
        	int count = (int)mem.Length;
        	
        	Console.WriteLine("Mem is at {1} and has {0} bytes.", count, mem.Position);
        	
        	int boundMatch = 0;
        	do
        	{
        		Console.WriteLine("Read {0} bytes.", count);
        		int bufferPos = 0;
        		
        		while(bufferPos < count)
        	    {
	        		int startBufferPos = bufferPos;
        	    	int deltaMatch = boundMatch;
        	    	
        	    	MatchByteSeq(buffer, ref bufferPos, count, boundary, ref boundMatch);
        	    	
        	    	deltaMatch = Math.Max(0, (boundMatch - deltaMatch));
        	    	int dataByteCount = (bufferPos - startBufferPos - deltaMatch);
        	    	
        	    	Console.Write("startBufferPos={0}; bufferPos={1}; boundMatch={2}; dataByteCount={3}; ", startBufferPos, bufferPos, boundMatch, dataByteCount);
	        	    outputStream.Write(buffer, startBufferPos, dataByteCount);
	        	    Console.WriteLine("Copied {0} bytes.", dataByteCount);
	        	    
	        	    if(boundMatch == boundary.Length)
	        	    {
	        	    	mode = ParsingMode.Epilogue;
	        	    	return;
	        	    }
        	    }
        	    Console.ReadKey(true);
            }
            while((count = inputStream.Read(buffer, 0, buffer.Length)) > 0);

			mode = ParsingMode.EndOfStream;
			
            Console.WriteLine("End of input stream.");
	    }
		
		private static bool ParseHeaderLine(IDictionary<string, string> headers, char[] text, int textLength)
		{
			int headerNameEnd = Array.IndexOf(text, ':', 0, textLength);
	
    		if(headerNameEnd != -1)
    		{
    			int valueLen = textLength - (headerNameEnd + 1);
        		string headerName = new string(text, 0, headerNameEnd);
        		string headerValue = new string(text, headerNameEnd + 1, valueLen).Trim();
        		        	        		
        		headers.Add(headerName, headerValue);
        		
        		return true;
    		}
    		else
    			return false;
		}
		
		private static void MatchByteSeq(byte[] buffer, ref int bufPos, int bufLen, byte[] seq, ref int matchPos)
	    {
	    	//Console.WriteLine("MatchByteSeq(bufPos = {0}, bufLen = {1}, matchPos = {2})", bufPos, bufLen, matchPos);
	        for(int i = bufPos; i < bufLen; i++, bufPos++)
	        {
	        	//Console.WriteLine("buffer[{0}] = '{1:X2}' seq[{2}] = '{3:X2}'", bufPos, buffer[bufPos], matchPos, seq[matchPos]);
	            if(buffer[bufPos] == seq[matchPos])
	            {
                    matchPos++;
					
	                if(matchPos == seq.Length)
	                {
	                	bufPos++;
	                    return;
	                }
                }
                else
                {
                	matchPos = 0;
                }
            }
            
            return;
	    }
    }
    
   	internal struct BufferSegment
	{
		public byte[] Data;
		public int Offset;
		public int Count;
	}
	
	internal enum ParsingMode
	{
		Preamble,
		Boundary,
		Headers,
		Data,
		Epilogue,
		EndOfStream
	}
}
