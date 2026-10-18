namespace CodeGenToTutorial.Core.Models;

// T5.3.3: controls how much explanation text the model is asked for per step. Standard is the
// default - it adds no extra instruction text, relying on the base preamble's wording.
public enum TutorialDepth
{
    Standard,
    Brief,
    Detailed,
}
