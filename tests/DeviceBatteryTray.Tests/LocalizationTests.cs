using System.Collections;
using System.Globalization;
using System.Resources;
using DeviceBatteryTray.Battery;
using DeviceBatteryTray.Services;
using DeviceBatteryTray.Ui;

namespace DeviceBatteryTray.Tests;

[TestClass]
public sealed class LocalizationTests
{
    // Tests never change Strings.Culture (shared static state, tests run in parallel); they read
    // the resources for an explicit culture instead.
    private static IReadOnlyDictionary<string, string> Read(string code, bool tryParents)
    {
        ResourceSet? set = Strings.Resources.GetResourceSet(CultureInfo.GetCultureInfo(code), createIfNotExists: true, tryParents);
        Assert.IsNotNull(set, $"no resources for '{code}'");
        return set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!);
    }

    [TestMethod]
    public void EveryLanguageTranslatesEveryString()
    {
        IReadOnlyDictionary<string, string> english = Read("en", tryParents: true);
        Assert.IsTrue(english.Count >= 20, "English resources should be embedded in the main assembly");

        foreach ((string code, _) in Languages.Supported.Where(l => l.Code != "en"))
        {
            IReadOnlyDictionary<string, string> translated = Read(code, tryParents: false);
            foreach (string key in english.Keys)
            {
                Assert.IsTrue(translated.TryGetValue(key, out string? text) && !string.IsNullOrWhiteSpace(text),
                    $"'{code}' is missing '{key}'");
            }

            Assert.AreEqual(english.Count, translated.Count, $"'{code}' has keys English does not have");
            StringAssert.Contains(translated["LogFile"], "{0}", $"'{code}' LogFile needs the path placeholder");
        }
    }

    [TestMethod]
    public void EveryDeviceKindHasAName()
    {
        IReadOnlyDictionary<string, string> english = Read("en", tryParents: true);
        foreach (DeviceKind kind in Enum.GetValues<DeviceKind>())
        {
            Assert.IsTrue(english.ContainsKey("Kind" + kind), $"missing Kind{kind}");
        }
    }

    [TestMethod]
    [DataRow(null, "ru-RU", "ru")]
    [DataRow(null, "uk-UA", "uk")]
    [DataRow(null, "be-BY", "be")]
    [DataRow(null, "de-AT", "de")]
    [DataRow(null, "fr-CA", "fr")]
    [DataRow(null, "es-MX", "es")]
    [DataRow(null, "en-GB", "en")]
    [DataRow(null, "zh-CN", "en")]
    [DataRow(null, "ja-JP", "en")]
    [DataRow(null, "pl-PL", "en")]
    [DataRow("de", "ru-RU", "de")]
    [DataRow("xx", "ru-RU", "ru")]
    public void ResolvesChosenThenSystemThenEnglish(string? chosen, string system, string expected) =>
        Assert.AreEqual(expected, Languages.Resolve(chosen, CultureInfo.GetCultureInfo(system)));

    [TestMethod]
    public void SettingsDropUnsupportedLanguage()
    {
        Assert.IsNull(new AppSettings { Language = "ja" }.Normalize().Language);
        Assert.AreEqual("uk", new AppSettings { Language = "uk" }.Normalize().Language);
    }

    [TestMethod]
    public void VersionShowsShortCommit()
    {
        Assert.AreEqual("1.2.3 (3f2a9c1)", AppVersion.Format("1.2.3+3f2a9c1d0e5b7a8c9d0e1f2a3b4c5d6e7f8a9b0c"));
        Assert.AreEqual("1.2.3", AppVersion.Format("1.2.3"));
    }
}
