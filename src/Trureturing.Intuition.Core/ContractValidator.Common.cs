using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trureturing.Intuition.Core;

public static partial class ContractValidator
{
public static void Validate(object artifact)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        switch (artifact)
        {
            case TruthReleaseVerificationReceipt value: Validate(value); break;
            case IntuitionRunRequest value: Validate(value); break;
            case TargetInterface value: Validate(value); break;
            case ResidualWitness value: Validate(value); break;
            case ResidualUniverse value: Validate(value); break;
            case CandidateEdit value: Validate(value); break;
            case IntuitionState value: Validate(value); break;
            case IntuitionProposal value: Validate(value); break;
            case IntuitionProposalSet value: Validate(value); break;
            case IntuitionCritique value: Validate(value); break;
            case IntuitionCritiqueSet value: Validate(value); break;
            case IntuitionValuation value: Validate(value); break;
            case IntuitionValuationSet value: Validate(value); break;
            case IntuitionAllocation value: Validate(value); break;
            case OwnerAuthorization value: Validate(value); break;
            case ResearchAttempt value: Validate(value); break;
            case IntuitionSettlement value: Validate(value); break;
            case IntuitionRelease value: Validate(value); break;
            case TemporalReplayCase value: Validate(value); break;
            case ReplayScore value: Validate(value); break;
            case CalibrationReport value: Validate(value); break;
            default: throw new InvalidOperationException($"No validator registered for {artifact.GetType().FullName}.");
        }
    }

    
}
