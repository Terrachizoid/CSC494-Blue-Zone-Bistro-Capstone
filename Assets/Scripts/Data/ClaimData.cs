// File responsibility: Authored evidence/approval metadata referenced by content. No gameplay scoring is calculated here.
// Draft content is not automatically an approved factual claim.

using UnityEngine;

[CreateAssetMenu(
    fileName = "NewClaim",
    menuName = "Blue Zone Bistro/Claim")]
public class ClaimData : ScriptableObject
{
    [Header("Identity")]
    public string id;

    [Header("Player-facing Text")]
    [TextArea]
    public string shortText;

    [TextArea(3, 8)]
    public string codexText;

    [Header("Evidence")]
    public string sourceTitle;

    [TextArea]
    public string sourceCitation;

    public string sourceUrl;
    public bool sourceChecked;

    [Header("Usage")]
    public ClaimUsageTag[] usageTags = { ClaimUsageTag.None };

    [Min(0)]
    public int tipDay;

    [Header("Approval")]
    public ApprovalStatus approvalStatus = ApprovalStatus.Pending;

    [TextArea]
    public string reviewNotes;

    public bool IsReadyForPlayerFacing =>
        approvalStatus == ApprovalStatus.Approved &&
        sourceChecked &&
        !string.IsNullOrWhiteSpace(shortText) &&
        !string.IsNullOrWhiteSpace(sourceUrl);
}
