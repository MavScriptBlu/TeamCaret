namespace CCOS.Web.Pages;

/// <summary>
/// What the shared "Remove?" confirmation dialog (_RemoveDialog.cshtml) says (CAR-56, CAR-58).
/// </summary>
/// <param name="Noun">What is being removed, in lowercase: "venue" or "event".</param>
/// <param name="HiddenFrom">Where it stops showing up, finishing "It will no longer appear in ...".</param>
public record RemoveDialogModel(string Noun, string HiddenFrom);
