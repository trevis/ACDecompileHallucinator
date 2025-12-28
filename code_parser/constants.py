"""
Filter rules for ignoring types and functions during processing.

This module defines prefixes, suffixes, and whitelists that control which
decompiled entities are processed or skipped.
"""
from typing import List


IGNORE_STRUCT_PREFIXES: List[str] = [
    '_', 'tag', '$', 'ATL', 'WinInet', 'Windows', 'std', 'SCARD', 'SB', 'RPC',
    'OPENCARD', 'MSXML', 'IMedia', 'IMem', 'IXML', 'ISynchronize', 'IRpc', 'IOle',
    'IPersist', 'IPin', 'IPipe', 'IProperty', 'IQua', 'IQeue', 'IRe', 'IOp', 'IMe',
    'IMa', 'IInt', 'IHttp', 'IGraph', 'IFil', 'IEn', 'ICreate', 'ICon',
    'D3DXTex', 'tMIXERCONTROLDETAILS', 'SYSTEM_', 'IDirect', 'DIDEVICE', 'sock',
    'tACMSTREAMHEADER', 'tWAVEFORMATEX', 'signed_', 'value_', 'val_', 'provider_info',
    'type_info', 'tree_desc_s', 'internal_state', 'streambuf', 'filebuf', 'ios',
    'ofstream', 'ostream', 'protoent', 'linger', 'istream', 'inflate_', 'in_addr',
    'ifstream', 'netent', 'localVar', 'Param', 'hostent', 'servent', 'get_storage_type',
    'fstream', 'fd_', 'exception', 'ct_data_s', 'config_s', '_com', 'XMLDOM',
    'URL_COMPONENTSW', 'TypeDescriptor', 'SHEPHANDLE', 'QzCComPtr', 'RASIPADDR',
    'QOS_OBJECT_HDR', 'POWER_ACTION_POLICY', 'PMD', 'NUMPARSE', 'MENUITEMTEMPLATE',
    'MIDIFILEHDR', 'D3D', 'AVL', 'z_stream_s', 'static_tree_desc_s', 'stat', 'pvalue',
    'TGA', 'TEMPEVENT', 'IWinInet', 'IWaitMultiple', 'IXTLRuntime', 'IType', 'IStdMarshalInfo',
    'IROTData', 'IPrintDialog', 'INTRACKSTATE', 'INFILESTATE', 'IMoniker', 'ILBM',
    'ICD', 'ICIDM', 'IBind', 'IAsync', 'IAuthenticate', 'IAdvice', 'IAddr',
    'HttpClient', 'HID', 'GRP', 'FLASHWINFO', 'DID', 'DDE', 'DIP', 'DIE', 'DIF', 'DLG',
    'DRVCONFIGINFOEX', 'CS_STUB_INFO', 'CSI', 'TGA', 'CPPEH_RECORD', 'CONFIRMSAFETY',
    'CM_Power_Data_s', 'BITMAPV', 'BATTERY_REPORTING_SCALE', 'ALP', 'AM_SEEKING',
    'CubeTexture', 'ADDRESS_MODE', 'BlitFormat'
]

IGNORE_STRUCT_SUFFIXES = ['_', '_tag']
STRUCT_WHITELIST = ['_IDClass', '_GUID', 'IPropertyBag', 'D3DPolyRender']

GLOBAL_FUNC_WHITELIST = []
IGNORE_GLOBAL_FUNC_PREFIXES = [
    'init_D3D', '_', 'c_D3D', 'AMD', 'AlphaConvert', 'BGR_', 'CC_', 'D3DX', 'Decode',
    'EB_', 'DoANSI', 'Fil', 'GXC', 'Gather_', 'Godot', 'OTHER_', 'x3d', 'x86_', 'user32',
    'template_', 'sub_', 'str', 'sse', 'own', 'kernel32', 'lstr', 'iterator', 'ijl', 'iQnt',
    'iDCT', 'operator', 'new', 'gdi', 'fQnt', 'fDCT', 'd3dx', 'advapi', 'a_', 'YCb', 'Y_',
    'US_', 'SerializeIntrusiveHashTable', 'SerializeHashTableData', 'RGB', 'Page', 'MIDL',
    'Encode_', 'EP_', 'DP_'
]   
IGNORE_GLOBAL_FUNC_SUFFIXES = []

FUNC_WHITELIST = []
IGNORE_FUNC_PREFIXES = [
    '_', 'ATL', 'D3DX', 'DX'
]   
IGNORE_FUNC_SUFFIXES = []

def should_ignore_class(name: str) -> bool:
    """Check if a class name should be ignored"""
    if name in STRUCT_WHITELIST:
        return False
    if any(name.startswith(p) for p in IGNORE_STRUCT_PREFIXES):
        return True
    if any(name.endswith(s) for s in IGNORE_STRUCT_SUFFIXES):
        return True
    return False

def should_ignore_global_method(name: str) -> bool:
    """Check if a class name should be ignored"""
    if name in GLOBAL_FUNC_WHITELIST:
        return False
    if any(name.startswith(p) for p in IGNORE_GLOBAL_FUNC_PREFIXES):
        return True
    if any(name.endswith(s) for s in IGNORE_GLOBAL_FUNC_SUFFIXES):
        return True
    return False

def should_ignore_class_method(name: str) -> bool:
    """Check if a class name should be ignored"""
    if name in FUNC_WHITELIST:
        return False
    if any(name.startswith(p) for p in IGNORE_FUNC_PREFIXES):
        return True
    if any(name.endswith(s) for s in IGNORE_FUNC_SUFFIXES):
        return True
    return False