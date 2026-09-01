namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
    public static void Validate<T>(T artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        switch (artifact)
        {
            case TopologyAtlasEvidencePublicationCoordinate value:
                Validate(value);
                return;
            case IntuitionTopologyAtlasEvidenceInputReceipt value:
                Validate(value);
                return;
            case IntuitionTopologyAtlasEvidenceInputCursor value:
                Validate(value);
                return;
            default:
                Validate((object)artifact);
                return;
        }
    }
}
