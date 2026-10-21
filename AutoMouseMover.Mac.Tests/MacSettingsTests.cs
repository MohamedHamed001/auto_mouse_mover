using AutoMouseMover.Mac.Services;
using AutoMouseMover.Utils;

namespace AutoMouseMover.Mac.Tests
{
    // Each test gets its own empty temporary folder, deleted afterwards
    public class MacSettingsTests : IDisposable
    {
        private readonly string mFolder = Path.Combine(Path.GetTempPath(), "amm-tests-" + Guid.NewGuid().ToString("N"));

        // Delete the temporary folder
        public void Dispose()
        {
            if (Directory.Exists(mFolder))
            {
                Directory.Delete(mFolder, true);
            }
        }

        // Write a settings file by hand (as a user or a damaged disk could)
        private void WriteFile(string cContent)
        {
            Directory.CreateDirectory(mFolder);
            File.WriteAllText(Path.Combine(mFolder, "settings.json"), cContent);
        }

        [Fact]
        public void Load_NoFile_UsesDefaults()
        {
            var settings = new MacSettings(mFolder);
            settings.Load();

            Assert.Equal(5, settings.MovingTime);
            Assert.Equal(5, settings.MovingPixel);
            Assert.False(settings.LeftClickAfterMoving);
            Assert.False(settings.ScheduleEnabled);
            Assert.Equal(new TimeSpan(9, 0, 0), settings.WorkStartTime);
            Assert.Equal(new TimeSpan(17, 0, 0), settings.WorkEndTime);
            Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday },
                         settings.WorkDays);
            Assert.Null(settings.PausedUntil);
            Assert.Equal("", settings.Language);
            Assert.Equal(eThemeChoice.SYSTEM, settings.Theme);
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            var saved = new MacSettings(mFolder);
            saved.MovingTime = 12;
            saved.MovingPixel = 40;
            saved.LeftClickAfterMoving = true;
            saved.ScheduleEnabled = true;
            saved.WorkStartTime = new TimeSpan(8, 30, 0);
            saved.WorkEndTime = new TimeSpan(16, 45, 0);
            saved.WorkDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Friday };
            saved.PausedUntil = DateTime.MaxValue;
            saved.Language = "it";
            saved.Theme = eThemeChoice.DARK;
            saved.Save();

            var loaded = new MacSettings(mFolder);
            loaded.Load();

            Assert.Equal(12, loaded.MovingTime);
            Assert.Equal(40, loaded.MovingPixel);
            Assert.True(loaded.LeftClickAfterMoving);
            Assert.True(loaded.ScheduleEnabled);
            Assert.Equal(new TimeSpan(8, 30, 0), loaded.WorkStartTime);
            Assert.Equal(new TimeSpan(16, 45, 0), loaded.WorkEndTime);
            Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Friday }, loaded.WorkDays);
            Assert.Equal(DateTime.MaxValue, loaded.PausedUntil);
            Assert.Equal("it", loaded.Language);
            Assert.Equal(eThemeChoice.DARK, loaded.Theme);
        }

        [Fact]
        public void Load_BadJson_UsesDefaults()
        {
            WriteFile("{not json");

            var settings = new MacSettings(mFolder);
            settings.Load();

            Assert.Equal(5, settings.MovingTime);
            Assert.False(settings.ScheduleEnabled);
        }

        [Fact]
        public void Load_BadFields_FallBackPerField()
        {
            WriteFile("{ \"ScheduleEnabled\": true, \"WorkStartTime\": \"25:00\", \"MovingTime\": 99, \"WorkDays\": \"Funday\", \"MovingPixel\": 30 }");

            var settings = new MacSettings(mFolder);
            settings.Load();

            Assert.Equal(new TimeSpan(9, 0, 0), settings.WorkStartTime);
            Assert.Equal(5, settings.MovingTime);
            Assert.Equal(30, settings.MovingPixel);   // valid fields are kept
            Assert.Empty(settings.WorkDays);
            Assert.Null(settings.CreateSchedule());    // no days: no schedule
        }

        [Fact]
        public void Save_CreatesFolder()
        {
            var settings = new MacSettings(mFolder);
            settings.Save();

            Assert.True(File.Exists(Path.Combine(mFolder, "settings.json")));
        }
    }
}
