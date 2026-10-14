using System.Reflection;
using Fragmo.Settings;

namespace Fragmo.Tests;

[Collection("Strings")]
public sealed class StringsTests : IDisposable
{
    public void Dispose() => Strings.Apply(UiLanguage.Portuguese);

    [Fact]
    public void Apply_Portuguese_UsesPortugueseTexts()
    {
        Strings.Apply(UiLanguage.Portuguese);
        Assert.Equal("Sair", Strings.MenuExit);
    }

    [Fact]
    public void Apply_English_UsesEnglishTexts()
    {
        Strings.Apply(UiLanguage.English);
        Assert.Equal("Exit", Strings.MenuExit);
    }

    [Fact]
    public void Apply_System_ResolvesToConcreteLanguage()
    {
        Strings.Apply(UiLanguage.System);
        Assert.NotEqual(UiLanguage.System, Strings.Current);
    }

    [Fact]
    public void AllTexts_AreFilledAndTranslated()
    {
        var props = typeof(Strings)
            .GetProperties(BindingFlags.Public | BindingFlags.Static)
            .Where(p => p.PropertyType == typeof(string))
            .ToList();

        Assert.NotEmpty(props);

        foreach (var p in props)
        {
            Strings.Apply(UiLanguage.Portuguese);
            string pt = (string)p.GetValue(null)!;
            Strings.Apply(UiLanguage.English);
            string en = (string)p.GetValue(null)!;

            Assert.False(string.IsNullOrWhiteSpace(pt), $"{p.Name} está vazio em português");
            Assert.False(string.IsNullOrWhiteSpace(en), $"{p.Name} is empty in English");
            Assert.True(pt != en, $"{p.Name} tem o mesmo texto nos dois idiomas: \"{pt}\"");
        }
    }
}
