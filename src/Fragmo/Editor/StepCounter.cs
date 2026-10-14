namespace Fragmo.Editor;

/// <summary>Próximo número da ferramenta de passos.</summary>
public static class StepCounter
{
    /// <summary>1 + o maior número existente, para que desfazer um passo reutilize o número.</summary>
    public static int Next(IReadOnlyList<IAnnotation> items)
    {
        int max = 0;
        foreach (IAnnotation item in items)
        {
            if (item is StepAnnotation step)
            {
                max = Math.Max(max, step.Number);
            }
        }
        return max + 1;
    }
}
