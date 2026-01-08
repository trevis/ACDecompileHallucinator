using System;

namespace ACDecompileParser.Shared.Lib.Models;

public class EnumMemberModel : BaseMemberModel
{
    public string Value { get; set; } = string.Empty;
    public int EnumTypeId { get; set; }
}
