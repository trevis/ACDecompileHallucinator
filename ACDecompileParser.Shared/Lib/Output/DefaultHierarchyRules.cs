using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACDecompileParser.Shared.Lib.Output.Rules;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Shared.Lib.Output;

public static class DefaultHierarchyRules
{
    public static List<IHierarchyRule> GetDefaultRules()
    {
        return new List<IHierarchyRule>
        {
            // templated types
            new FqnMatchRule("^(LongHash|InterfacePtr|PS|NILI|UI64|SmartArray|List|Hash|SmartBuffer|Intrusive|AutoGrow|LongNI|PackableL|DLList|PackableHa|RefCountIUnknown)","Lib/Templates/", NamespaceBehavior.StripAll, 1),
            
            // misc
            new FqnMatchRule("^InterfaceInfo", "Lib/Templates", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Types?::", "Lib/Types/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^_(GUID|tag)", "Lib/Types/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("QualityType", "Lib/Types/QualityTypes/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("StringTableMetaLanguage", "Dats/", NamespaceBehavior.KeepAll, 0),
            
            /*
             * Lib stuff, like helpers, base types, etc.
             */
            // Turbine Debug stuff
            new FqnMatchRule("^(Turbine|You_Need)", "Lib/Turbine/", NamespaceBehavior.KeepAll, 0),
            new FqnMatchRule("^AC1(Legacy|Modern)", "Lib/", NamespaceBehavior.KeepAll, 0),
            new FqnMatchRule("^Logger", "Lib/", NamespaceBehavior.KeepAll, 0),
            new FqnMatchRule("^(PlatformString|InputStream)", "Lib/", NamespaceBehavior.KeepAll, 0),
            new FqnMatchRule("^(Profiler|PerfMon|BudgetStat)", "Lib/Profiler", NamespaceBehavior.KeepAll, 0),
            new FqnMatchRule("(DbgHelp|DbgReport|Debug)", "Lib/Debug", NamespaceBehavior.KeepAll, 0),
            new FqnMatchRule("^(Logger|IRpc|IStream|IStor|ISeq|INon|Class)", "Lib/", NamespaceBehavior.KeepAll, 0),
            
            // DBObjs
            new BaseClassRule("SerializeUsingPackDBObj", "Dats/DBObjs/", NamespaceBehavior.KeepAll, 1),
            new BaseClassRule("DBObj", "Dats/DBObjs/", NamespaceBehavior.KeepAll, 1),
            
            // Networking
            new BaseClassRule("PackObj", "Net/Types/", NamespaceBehavior.KeepAll, 1),
            new BaseClassRule("COptionalHeader", "Net/Headers/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("OptionalHeader|CServerSwitchStruct", "Net/Headers/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(PackHeader|accountID|ActionNode)", "Net/Types/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^(DDD_)", "Net/Messages/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Connection|Packet|^Blob|^(?!.*Scene).*Net.*$|CLink", "Net/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Crypto|Isaaq)", "Net/Crypto/", NamespaceBehavior.KeepAll, 1),
            
            // Physics
            new FqnMatchRule("^(C?Physics|COLLISION|AtkCollisionProfile)", "Physics/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^(CObjCell|EtherealWeenie)", "Physics/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^BSP", "Physics/BSP", NamespaceBehavior.KeepAll, 1),
            
            // Game Systems 
            new FqnMatchRule("System$", "Game/Systems", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Chat|TextTag)", "Game/Chat/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("SmartBox", "Game/SmartBox", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^Ambient(Sound)?$", "Game/SmartBox", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(CharGen|_CG$)", "Game/CharGen", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Allegiance", "Game/Allegiance", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Fellow", "Game/Fellowship", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Piece|Chess|MiniGame)", "Game/Chess", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Attack|Combat|Damage|PowerBar)", "Game/Combat", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Weenie|AlreadyRunning|APIManager)", "Game/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^(ACC(Factory|Object|md))", "Game/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Plugin", "Game/Plugins", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Trade", "Game/Trade", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Spell|Enchant)", "Game/Spells", NamespaceBehavior.KeepAll, 1),
            
            // Input
            new FqnMatchRule("^(C?Input|Device|CommandInterp|I?Keystone|ActionState|ArgumentParser|ATTNAMESSTRUCT|CommandLineArg)", "Input/", NamespaceBehavior.KeepAll, 1),
            
            
            // dat types
            new FqnMatchRule("^(CScene|CSound|CTerrain)", "Dats/Types/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^(CAnimHook|ActionMapValue|AFrame)", "Dats/Types/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Ambient(STB|Sound)Desc|SoundType)", "Dats/Types/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("BuildInfo", "Dats/Types/", NamespaceBehavior.KeepAll, 1),
            new BaseClassRule("CAnimHook", "Dats/Types/AnimHooks/", NamespaceBehavior.KeepAll, 1),
            new MemberNameRule("GetSubDataIDs", "Dats/Types/", NamespaceBehavior.KeepAll, 1),
            
            // Media Handling
            new FqnMatchRule("^(MD_|C?Media)", "UI/Media/", NamespaceBehavior.KeepAll, 1),
            
            // Rendering
            new FqnMatchRule("D3D", "Rendering/D3D", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Camera", "Rendering/Camera", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(Render|DepthTest|Vertex|Blit|Blend|Graphics|CullMode)", "Rendering/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^(view_|Alpha)", "Rendering/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^(?!.*Hook$)Anim", "Rendering/Animation", NamespaceBehavior.KeepAll, 1),
            
            // UI Elements and Containers
            new BaseClassRule("UIElement", "UI/Elements/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("^((gm)?UI|Dialog)", "UI/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("(UI|InfoRegion)$", "UI/", NamespaceBehavior.KeepAll, 1),
            
            // dat file structures
            new FqnMatchRule("^DB(O?Cache|Level|TypeDef)", "Dats/", NamespaceBehavior.StripAll, 0),
            new FqnMatchRule("^DBUpdateType|(CL(Cache|Block))", "Dats/", NamespaceBehavior.StripAll, 0, "DBUpdateType"),
            new FqnMatchRule("^PropertyDatFileType", "Dats/", NamespaceBehavior.StripAll, 0, "PropertyDatFileType"),
            new FqnMatchRule("^(DBObjGrabber|DATFILE_|Archive|DBObj|TransientArchive)", "Dats/", NamespaceBehavior.StripAll, 0),
            new FqnMatchRule("^(cache_)", "Dats/", NamespaceBehavior.StripAll, 0),
            new FqnMatchRule("(TransactInfo)", "Dats/Transactions/", NamespaceBehavior.StripAll, 0),
            new FqnMatchRule("^Disk", "Dats/Disk/", NamespaceBehavior.StripAll, 0),
            new FqnMatchRule("^BT", "Dats/BTree", NamespaceBehavior.StripAll, 0),
            
            // Async Operations
            new FqnMatchRule("Async", "Lib/Async/", NamespaceBehavior.KeepAll, 0),
            
            // Cleanup
            new FqnMatchRule("Client", "Game/", NamespaceBehavior.KeepAll, 1),
            new FqnMatchRule("Property", "Game/Properties/", NamespaceBehavior.KeepAll, 1),
        };
    }
}
