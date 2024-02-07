﻿using System.Text.Json;

namespace Terminal.Gui.ConfigurationTests; 

public class AppScopeTests {
    public static readonly JsonSerializerOptions _jsonOptions = new () {
                                                                           Converters = {
                                                                               //new AttributeJsonConverter (),
                                                                               //new ColorJsonConverter ()
                                                                           }
                                                                       };

    [Fact]
    [AutoInitShutdown]
    public void Apply_ShouldApplyUpdatedProperties () {
        ConfigurationManager.Reset ();
        Assert.Null (AppSettingsTestClass.TestProperty);
        Assert.NotEmpty (ConfigurationManager.AppSettings);
        Assert.Null (ConfigurationManager.AppSettings["AppSettingsTestClass.TestProperty"].PropertyValue);

        AppSettingsTestClass.TestProperty = true;
        ConfigurationManager.Reset ();
        Assert.True (AppSettingsTestClass.TestProperty);
        Assert.NotEmpty (ConfigurationManager.AppSettings);
        Assert.Null (ConfigurationManager.AppSettings["AppSettingsTestClass.TestProperty"].PropertyValue as bool?);

        ConfigurationManager.AppSettings["AppSettingsTestClass.TestProperty"].PropertyValue = false;
        Assert.False (ConfigurationManager.AppSettings["AppSettingsTestClass.TestProperty"].PropertyValue as bool?);

        // ConfigurationManager.Settings should NOT apply theme settings
        ConfigurationManager.Settings.Apply ();
        Assert.True (AppSettingsTestClass.TestProperty);

        // ConfigurationManager.Themes should NOT apply theme settings
        ThemeManager.Themes![ThemeManager.SelectedTheme]!.Apply ();
        Assert.True (AppSettingsTestClass.TestProperty);

        // ConfigurationManager.AppSettings should NOT apply theme settings
        ConfigurationManager.AppSettings.Apply ();
        Assert.False (AppSettingsTestClass.TestProperty);
    }

    [Fact]
    public void TestNullable () {
        AppSettingsTestClass.TestProperty = null;
        Assert.Null (AppSettingsTestClass.TestProperty);

        ConfigurationManager.Initialize ();
        ConfigurationManager.GetHardCodedDefaults ();
        ConfigurationManager.Apply ();
        Assert.Null (AppSettingsTestClass.TestProperty);

        AppSettingsTestClass.TestProperty = true;
        ConfigurationManager.Initialize ();
        ConfigurationManager.GetHardCodedDefaults ();
        Assert.NotNull (AppSettingsTestClass.TestProperty);
        ConfigurationManager.Apply ();
        Assert.NotNull (AppSettingsTestClass.TestProperty);
    }

    [Fact]
    public void TestSerialize_RoundTrip () {
        ConfigurationManager.Reset ();

        AppScope initial = ConfigurationManager.AppSettings;

        string serialized = JsonSerializer.Serialize (ConfigurationManager.AppSettings, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AppScope> (serialized, _jsonOptions);

        Assert.NotEqual (initial, deserialized);
        Assert.Equal (deserialized.Count, initial.Count);
    }

    public class AppSettingsTestClass {
        [SerializableConfigurationProperty (Scope = typeof (AppScope))]
        public static bool? TestProperty { get; set; }
    }
}
