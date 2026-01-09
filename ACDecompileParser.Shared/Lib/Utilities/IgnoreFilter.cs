using System;
using System.Collections.Generic;
using System.Linq;

namespace ACDecompileParser.Shared.Lib.Utilities;

public static class IgnoreFilter
{
    public static readonly HashSet<string> IgnorePrefixes = new HashSet<string>
    {
        "_", "tag", "ATL", "WinInet", "Windows", "std", "SCARD", "SB", "RPC",
        "OPENCARD", "MSXML", "IMedia", "IMem", "IXML", "ISynchronize", "IOle",
        "IPersist", "IPin", "IPipe", "IProperty", "IQua", "IQeue", "IRe", "IOp", "IMe",
        "IMa", "IInt", "IHttp", "IGraph", "IFil", "IEn", "ICreate", "ICon",
        "D3DXTex", "tMIXERCONTROLDETAILS", "SYSTEM_", "IDirect", "DIDEVICE", "sock",
        "tACMSTREAMHEADER", "tWAVEFORMATEX", "signed_", "value_", "val_", "provider_info",
        "type_info", "tree_desc_s", "internal_state", "streambuf", "filebuf", "ios",
        "ofstream", "ostream", "protoent", "linger", "istream", "inflate_", "in_addr",
        "ifstream", "netent", "localVar", "Param", "hostent", "servent", "get_storage_type",
        "fstream", "fd_", "exception", "ct_data_s", "config_s", "_com", "XMLDOM",
        "URL_COMPONENTSW", "TypeDescriptor", "SHEPHANDLE", "QzCComPtr", "RASIPADDR",
        "QOS_OBJECT_HDR", "POWER_ACTION_POLICY", "PMD", "NUMPARSE", "MENUITEMTEMPLATE",
        "MIDIFILEHDR", "AVL", "z_stream_s", "static_tree_desc_s", "stat", "pvalue",
        "TGA", "TEMPEVENT", "IWinInet", "IWaitMultiple", "IXTLRuntime", "IType", "IStdMarshalInfo",
        "IROTData", "IPrintDialog", "INTRACKSTATE", "INFILESTATE", "IMoniker", "ILBM",
        "ICD", "ICIDM", "IBind", "IAsync", "IAuthenticate", "IAdvice", "IAddr",
        "HttpClient", "HID", "GRP", "FLASHWINFO", "DID", "DE", "DIP", "DIE", "DIF", "DLG",
        "DRVCONFIGINFOEX", "CS_STUB_INFO", "CSI", "TGA", "CPPEH_RECORD", "CONFIRMSAFETY",
        "CM_Power_Data_s", "BITMAPV", "BATTERY_REPORTING_SCALE", "ALP", "AM_SEEKING",
        "CubeTexture", "ADDRESS_MODE", "BlitFormat", "_bstr_t", "_STL", "XLAT_SIDE",
        "VXDINSTANCE", "WTimer", "WLogSystem", "STUB_PHASE", "SQFL", "ReplacesCorHdrNumericDefines",
        "RDBBitmask", "PROXY_PHASE", "POWER_INFORMATION_LEVEL", "POWER_ACTION",
        "PIDMSI_STATUS_VALUE", "OfflineFolderStatus", "MySTLSortFunction_LRU",
        "LSA_FOREST_TRUST_RECORD_TYPE", "LMINMAX", "LIST_ENTRY", "LATENCY_TIME",
        "IUrlMon", "IThumbnailExtractor", "ITimeAndNoticeControl", "ISurrogate",
        "ISupportErrorInfo", "ISoftDistExt", "IServiceProvider", "IRootStorage",
        "IRunnableObject", "IRunningObject", "CSeekingPassThru", "ISeekingPassThru",
        "IPSFactoryBuffer", "IProgressNotify", "IProcessInitControl", "IPropertyBag",
        "IParseDisplayName", "IObjectWithSite", "IObjectFactory", "INTERNET_SCHEME",
        "IMultiQI", "AsyncIMultiQI", "IMAGE_LOAD_CONFIG_DIRECTORY", "IMAGE_COR20_HEADER",
        "IMAGE_AUX_SYMBOL", "ILockBytes", "ILayoutStorage", "IForegroundTransfer",
        "IGlobalInterfaceTable", "IExternalConnection", "IError", "IDummyHICONIncluder",
        "ICSETSTATUSPROC", "IComThreadingInfo", "ICodeInstall", "IWindowForBindingUI",
        "IClassFactory", "ICatRegister", "ICatalogFileInfo", "ICancelMethodCalls",
        "ICallFactory", "IBlockingLock", "IBaseFilter", "IAdviseSink", "FOURCCMap",
        "CMsg", "CEm", "CEd", "CAutoUsingOutputPin", "BIDI_TYPE", "IMPORT_OBJECT_HEADER",
        "FallocPool", "IntrusiveLFData", "Intrusive_MW", "ANON_OBJECT_HEADER",
        "ANSIColorStatus", "ARRAY_INFO", "CHid'", "CBase", "CDeferredCommand", "IDeferredCommand",
        "CDispParams", "CDynamicOutputPin", "CEnum"
    };

    public static readonly HashSet<string> IgnoreSuffixes = new HashSet<string>
    {
        "_", "_tag"
    };

    public static readonly HashSet<string> Whitelist = new HashSet<string>
    {
        "_IDClass", "D3DPolyRender", "_GUID", "IFileNodeName", "IFileNodeName_vtbl",
        "_tagDataID", "_tag", "_tagVersionHandle", "_tagCellID", "tagPOINT", "CEmoteTable",
        "tagRECT", "_D3DPOOL", "_D3DFORMAT", "tWAVEFORMATEX"
    };

    /// <summary>
    /// Determines if a type should be ignored based on its fully qualified name
    /// </summary>
    /// <param name="fullyQualifiedName">The fully qualified name of the type</param>
    /// <returns>True if the type should be ignored, false otherwise</returns>
    public static bool ShouldIgnoreType(string fullyQualifiedName)
    {
        if (string.IsNullOrEmpty(fullyQualifiedName))
            return false;

        // Check whitelist first - whitelisted types are never ignored
        if (Whitelist.Contains(fullyQualifiedName))
            return false;

        // Check prefixes
        foreach (string prefix in IgnorePrefixes)
        {
            if (fullyQualifiedName.StartsWith(prefix))
                return true;
        }

        // Check suffixes
        foreach (string suffix in IgnoreSuffixes)
        {
            if (fullyQualifiedName.EndsWith(suffix))
                return true;
        }

        return false;
    }
}
