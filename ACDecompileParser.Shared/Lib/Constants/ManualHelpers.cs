namespace ACDecompileParser.Shared.Lib.Constants;

/// <summary>
/// Contains manual helper classes that are output to the Manual/ subdirectory
/// when generating C# bindings.
/// </summary>
public static class ManualHelpers
{
    /// <summary>
    /// Dictionary of manual helper class names to their content.
    /// Each entry will be output to Manual/{ClassName}.cs
    /// </summary>
    public static readonly Dictionary<string, string> Helpers = new()
    {
        ["GlobalTypes"] = """
              namespace ACBindings.Internal;

              using HRESULT = int;
              """,
        ["AC1Legacy::PSRefBuffer"] = """
             namespace ACBindings.Internal.AC1Legacy;
             public unsafe struct PSRefBuffer__char
             {
                 public Turbine_RefCount _ref;
                 public int m_len;
                 public uint m_size;
                 public uint m_hash;
                 public fixed sbyte m_data[512];
             }
             
             public unsafe struct PSRefBuffer__ushort
             {
                 public Turbine_RefCount _ref;
                 public int m_len;
                 public uint m_size;
                 public uint m_hash;
                 public fixed ushort m_data[256];
             }
             """,
        ["AC1Legacy::PStringBase"] = """
             using System.Runtime.CompilerServices;
             namespace ACBindings.Internal.AC1Legacy;
             
             public unsafe struct PStringBase_char
             {
                 public PSRefBuffer__char* m_buffer;
             
                 public PStringBase_char(string str)
                 {
                     m_buffer = *s_NullBuffer;
                     __Ctor((PStringBase_char*)Unsafe.AsPointer(ref this), System.Text.Encoding.ASCII.GetBytes(str + '\0'));
                 }
             
                 public override string ToString()
                 {
                     if (m_buffer == null || m_buffer->m_len == 0) return "null";
                     return new string((sbyte*)m_buffer->m_data, 0, m_buffer->m_len - 1);
                 }
             
                 // Implicit conversions - char version
                 public static implicit operator PStringBase_char(string inStr) => new(inStr);
             
                 // Constructors (flattened)
                 public static delegate* unmanaged[Thiscall]<PStringBase_char*, byte[], void> __Ctor
                     = (delegate* unmanaged[Thiscall]<PStringBase_char*, byte[], void>)0x0048C3E0;
             
                 public static delegate* unmanaged[Thiscall]<PStringBase_char*, int, void> __Ctor_int32
                     = (delegate* unmanaged[Thiscall]<PStringBase_char*, int, void>)0x004ADBA0;
             
                 // Methods - only char-specific ones
                 public uint GetPackSize()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, uint>)0x004FD1F0)(ref this);
             
                 public uint Pack(void** addr, uint size)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, void**, uint, uint>)0x004FD290)(ref this, addr, size);
             
                 public int UnPack(void** addr, uint size)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, void**, uint, int>)0x004FD460)(ref this, addr, size);
             
                 public void allocate_ref_buffer(uint len)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, uint, void>)0x00403560)(ref this, len);
             
                 public void append_n_chars(byte* str, uint count)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, byte*, uint, void>)0x004910C0)(ref this, str, count);
             
                 public void break_reference()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, void>)0x00411870)(ref this);
             
                 public void clear()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, void>)0x004AB990)(ref this);
             
                 public int cmp(PStringBase_char* rhs, int case_sensitive)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, PStringBase_char*, int, int>)0x004ABA90)(ref this, rhs, case_sensitive);
             
                 public uint compute_hash()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, uint>)0x004FE440)(ref this);
             
                 public byte eq(PStringBase_char* rhs, int case_sensitive)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, PStringBase_char*, int, byte>)0x004AC350)(ref this, rhs, case_sensitive);
             
                 public int find_substring(PStringBase_char* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, PStringBase_char*, int>)0x00542EA0)(ref this, str);
             
                 public int replace(PStringBase_char* search, PStringBase_char* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, PStringBase_char*, PStringBase_char*, int>)0x00566D10)(ref this, search, str);
             
                 public void set(byte* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, byte*, void>)0x004034C0)(ref this, str);
             
                 public PStringBase__ushort* to_wpstring(PStringBase__ushort* result, ushort i_sourceCodePage)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, PStringBase__ushort*, ushort, PStringBase__ushort*>)0x005571C0)(ref this, result, i_sourceCodePage);
             
                 public void trim(int pre, int post, PStringBase_char filter)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, int, int, PStringBase_char, void>)0x0056F9A0)(ref this, pre, post, filter);
             
                 public int vsprintf(char* fmt, char* args)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase_char, byte*, byte*, int>)0x00487480)(ref this, fmt, args);
             
                 // Globals - char version
                 public static PSRefBuffer__char** s_NullBuffer = (PSRefBuffer__char**)0x008EF11C;
                 public static PStringBase_char* null_string = (PStringBase_char*)0x008EF120;
                 public static PStringBase_char* whitespace_string = (PStringBase_char*)0x008EF124;
             }
             
             public unsafe struct PStringBase__ushort
             {
                 public PSRefBuffer__ushort* m_buffer;
             
                 public override string ToString()
                 {
                     if (m_buffer == null || m_buffer->m_len == 0) return "null";
                     return new string((char*)m_buffer->m_data, 0, m_buffer->m_len - 1);
                 }
             
                 // Implicit conversions - only stub (no direct int → wide-string in original)
                 public static implicit operator PStringBase__ushort(string inStr)
                 {
                     PStringBase__ushort ret;
                     ret.m_buffer = *s_NullBuffer_w;
             
                     ushort[] buf = new ushort[inStr.Length];
                     for (int i = 0; i < inStr.Length; i++)
                         buf[i] = inStr[i];
             
                     __Ctor_16((PStringBase__ushort*)&ret, buf);
                     return ret;
                 }
             
                 // Constructor (wide version)
                 public static delegate* unmanaged[Thiscall]<PStringBase__ushort*, ushort[], void> __Ctor_16
                     = (delegate* unmanaged[Thiscall]<PStringBase__ushort*, ushort[], void>)0x005444D0;
             
                 // Methods - only ushort-specific ones
                 public void allocate_ref_buffer(uint len)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, uint, void>)0x00543680)(ref this, len);
             
                 public void set(ushort* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, ushort*, void>)0x0055F580)(ref this, str);
             
                 public PStringBase_char* to_spstring(PStringBase_char* result, ushort i_targetCodePage)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, PStringBase_char*, ushort, PStringBase_char*>)0x00546290)(ref this, result, i_targetCodePage);
             
                 // Globals - ushort version
                 public static PSRefBuffer__ushort** s_NullBuffer_w = (PSRefBuffer__ushort**)0x008EF12C;
                 public static PStringBase__ushort* null_string_w = (PStringBase__ushort*)0x008EF130;
                 public static PStringBase__ushort* whitespace_string_w = (PStringBase__ushort*)0x008EF134;
             }
             """,
        ["PSRefBufferCharData"] = """
              namespace ACBindings.Internal;
              public unsafe struct PSRefBufferCharData__sbyte
              {
                  public fixed sbyte m_data[16];
              }
              
              public unsafe struct PSRefBufferCharData__ushort
              {
                  public fixed ushort m_data[16];
              }
              """,
        ["PStringBase"] = """
             using System.Runtime.CompilerServices;
             
             namespace ACBindings.Internal;
             
             public unsafe struct PStringBase__sbyte
             {
                 public static PSRefBufferCharData__sbyte** s_NullBuffer = (PSRefBufferCharData__sbyte**)0x00818344;
                 public static PStringBase__sbyte* null_string = (PStringBase__sbyte*)0x008183B4;
                 
                 public PSRefBufferCharData__sbyte* m_charbuffer;
             
                 public PStringBase__sbyte(string str)
                 {
                     m_charbuffer = *s_NullBuffer;
                     __Ctor((PStringBase__sbyte*)Unsafe.AsPointer(ref this), System.Text.Encoding.ASCII.GetBytes(str + '\0'));
                 }
                 
                 public static delegate* unmanaged[Thiscall]<PStringBase__sbyte*, byte[], void> __Ctor = (delegate* unmanaged[Thiscall]<PStringBase__sbyte*, byte[], void>)0x00401340;
             
                 public static implicit operator PStringBase__sbyte(string inStr) => new PStringBase__sbyte(inStr);
                 
                 public override string ToString()
                 {
                     int len = 0;
                     if (m_charbuffer != null)
                     {
                         sbyte* ptr = (sbyte*)m_charbuffer->m_data;
                         while (ptr[len] != 0) len++;
                     }
                     return new string((sbyte*)m_charbuffer, 0, len);
                 }
             
                 public void SetAtIndex(uint nIndex, sbyte zCharacter)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, uint, sbyte, void>)0x00408770)(ref this, nIndex, zCharacter);
             
                 public byte allocate(uint num_chars)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, uint, byte>)0x00408D90)(ref this, num_chars);
             
                 public byte allocate_ref_buffer(uint len)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, uint, byte>)0x00401280)(ref this, len);
             
                 public void append_n_chars(sbyte* str, uint count)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, sbyte*, uint, void>)0x00404EF0)(ref this, str, count);
             
                 public void append_string(PStringBase__sbyte* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, PStringBase__sbyte*, void>)0x004064E0)(ref this, str);
             
                 public void append_uint32(uint num)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, uint, void>)0x0040F110)(ref this, num);
             
                 public void break_reference()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, void>)0x004080C0)(ref this);
             
                 public void clear()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, void>)0x00404CD0)(ref this);
             
                 public int cmp(PStringBase__sbyte* rhs, byte case_sensitive)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, PStringBase__sbyte*, byte, int>)0x00404B40)(ref this, rhs, case_sensitive);
             
                 public byte eq(PStringBase__sbyte* rhs, byte case_sensitive)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, PStringBase__sbyte*, byte, byte>)0x00404D20)(ref this, rhs, case_sensitive);
             
                 public int find_substring(PStringBase__sbyte* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, PStringBase__sbyte*, int>)0x00404D40)(ref this, str);
             
                 public uint hash()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, uint>)0x004134B0)(ref this);
             
                 public int replace(PStringBase__sbyte* search, PStringBase__sbyte* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, PStringBase__sbyte*, PStringBase__sbyte*, int>)0x004053A0)(ref this, search, str);
             
                 public void set(PStringBase__sbyte* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, PStringBase__sbyte*, void>)0x00401700)(ref this, str);
             
                 public void set(sbyte* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, sbyte*, void>)0x00405000)(ref this, str);
             
                 public int to_int32()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, int>)0x00429A50)(ref this);
             
                 public uint to_uint32()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, uint>)0x00404D70)(ref this);
             
                 public PStringBase__ushort* to_wpstring(PStringBase__ushort* result, ushort i_sourceCodePage)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, PStringBase__ushort*, ushort, PStringBase__ushort*>)0x00403350)(ref this, result, i_sourceCodePage);
             
                 public void trim(byte pre, byte post, PStringBase__sbyte filter)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, byte, byte, PStringBase__sbyte, void>)0x00435720)(ref this, pre, post, filter);
             
                 public int vsprintf(sbyte* fmt, sbyte* args)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__sbyte, sbyte*, sbyte*, int>)0x00402390)(ref this, fmt, args);
             }
             
             public unsafe struct PStringBase__ushort
             {
                 public static PSRefBufferCharData__ushort** s_NullBuffer_w = (PSRefBufferCharData__ushort**)0x00818340;
                 public static PStringBase__ushort* null_string_w = (PStringBase__ushort*)0x0083774C;
                 public static PStringBase__ushort* whitespace_string_w = (PStringBase__ushort*)0x00837750;
                 
                 public PSRefBufferCharData__ushort* m_charbuffer;
             
                 public PStringBase__ushort(string str)
                 {
                     m_charbuffer = *s_NullBuffer_w;
                     __Ctor((PStringBase__ushort*)Unsafe.AsPointer(ref this), System.Text.Encoding.ASCII.GetBytes(str + '\0'));
                 }
                 
                 public static delegate* unmanaged[Thiscall]<PStringBase__ushort*, byte[], void> __Ctor = (delegate* unmanaged[Thiscall]<PStringBase__ushort*, byte[], void>)0x00401A60;
             
                 public static implicit operator PStringBase__ushort(string inStr) => new PStringBase__ushort(inStr);
             
                 public override string ToString()
                 {
                     int len = 0;
                     if (m_charbuffer != null)
                     {
                         ushort* ptr = (ushort*)m_charbuffer->m_data;
                         while (ptr[len] != 0) len++;
                     }
                     return new string((char*)m_charbuffer, 0, len);
                 }
             
                 public void SetAtIndex(uint nIndex, ushort zCharacter)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, uint, ushort, void>)0x004089F0)(ref this, nIndex, zCharacter);
             
                 public byte allocate(uint num_chars)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, uint, byte>)0x00408EC0)(ref this, num_chars);
             
                 public byte allocate_ref_buffer(uint len)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, uint, byte>)0x004022D0)(ref this, len);
             
                 public void append_int32(int num)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, int, void>)0x0047B520)(ref this, num);
             
                 public void append_n_chars(ushort* str, uint count)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, ushort*, uint, void>)0x00402490)(ref this, str, count);
             
                 public void append_string(PStringBase__ushort* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, PStringBase__ushort*, void>)0x00402790)(ref this, str);
             
                 public void append_string(ushort* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, ushort*, void>)0x0040B8F0)(ref this, str);
             
                 public void break_reference()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, void>)0x00408390)(ref this);
             
                 public void clear()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, void>)0x0040B220)(ref this);
             
                 public int replace(PStringBase__ushort* search, PStringBase__ushort* str)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, PStringBase__ushort*, PStringBase__ushort*, int>)0x0040D870)(ref this, search, str);
             
                 public uint to_uint32()
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, uint>)0x00478B80)(ref this);
             
                 public PStringBase__ushort* to_spstring(PStringBase__ushort* result, ushort i_targetCodePage)
                     => ((delegate* unmanaged[Thiscall]<ref PStringBase__ushort, PStringBase__ushort*, ushort, PStringBase__ushort*>)0x00408FD0)(ref this, result, i_targetCodePage);
             }
             """,
    };
}
