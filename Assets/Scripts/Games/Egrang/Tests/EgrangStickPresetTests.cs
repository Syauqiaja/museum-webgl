using NUnit.Framework;

namespace Museum.Games.Egrang.Tests
{
    /// <summary>
    /// Guards the shipped difficulty curve. The three stilts are supposed to line up with their
    /// cross-sectional areas — square 64 cm² easiest, round 50,24 cm² in the middle, triangle
    /// 32 cm² hardest — and that ordering is easy to break while retuning one number at a time.
    /// </summary>
    public class EgrangStickPresetTests
    {
        [Test]
        public void EveryPreset_BuildsAValidZoneTable()
        {
            foreach (EgrangStickPreset preset in EgrangStickPresets.All)
            {
                Assert.That(preset.BuildZones().Validate(out string error), Is.True,
                            $"{preset.Shape}: {error}");
            }
        }

        [Test]
        public void EveryPreset_IsSymmetricAboutTheCentre()
        {
            foreach (EgrangStickPreset preset in EgrangStickPresets.All)
            {
                SkillCheckZones zones = preset.BuildZones();

                // An off-centre green would score the two sweep directions differently.
                //
                // Sampled off the 0.01 grid on purpose. Bands are half-open — [start, end) — so a
                // sample landing exactly on a band edge scores Full on the low side and Half on the
                // high side by construction, whatever the table. That is a property of the interval
                // convention, not the asymmetry this test is looking for, and a preset whose green
                // half-width happens to be a round hundredth would otherwise fail for no reason.
                for (float offset = 0.005f; offset < 0.5f; offset += 0.01f)
                {
                    Assert.That(zones.Evaluate(0.5f + offset), Is.EqualTo(zones.Evaluate(0.5f - offset)),
                                $"{preset.Shape} is asymmetric at ±{offset:0.00}");
                }
            }
        }

        [Test]
        public void EveryPreset_ScoresTheCentreAsAFullStep()
        {
            foreach (EgrangStickPreset preset in EgrangStickPresets.All)
            {
                Assert.That(preset.BuildZones().Evaluate(0.5f), Is.EqualTo(EgrangStepResult.Full),
                            $"{preset.Shape} has no green at the centre of the track");
            }
        }

        [Test]
        public void EveryPreset_ScoresBothEdgesAsAFail()
        {
            foreach (EgrangStickPreset preset in EgrangStickPresets.All)
            {
                SkillCheckZones zones = preset.BuildZones();
                Assert.That(zones.Evaluate(0f), Is.EqualTo(EgrangStepResult.Fail), $"{preset.Shape} left edge");
                Assert.That(zones.Evaluate(1f), Is.EqualTo(EgrangStepResult.Fail), $"{preset.Shape} right edge");
            }
        }

        [Test]
        public void GreenWidth_ShrinksAsTheCrossSectionShrinks()
        {
            float persegi = EgrangStickPresets.Persegi.BuildZones().TotalWidth(EgrangStepResult.Full);
            float lingkaran = EgrangStickPresets.Lingkaran.BuildZones().TotalWidth(EgrangStepResult.Full);
            float segitiga = EgrangStickPresets.Segitiga.BuildZones().TotalWidth(EgrangStepResult.Full);

            Assert.That(persegi, Is.GreaterThan(lingkaran), "square should be more forgiving than round");
            Assert.That(lingkaran, Is.GreaterThan(segitiga), "round should be more forgiving than triangular");
        }

        [Test]
        public void ForgivingWidth_ShrinksAsTheCrossSectionShrinks()
        {
            // Green plus yellow: the share of the sweep that moves the player at all.
            float Forgiving(EgrangStickPreset preset)
            {
                SkillCheckZones zones = preset.BuildZones();
                return zones.TotalWidth(EgrangStepResult.Full) + zones.TotalWidth(EgrangStepResult.Half);
            }

            Assert.That(Forgiving(EgrangStickPresets.Persegi), Is.GreaterThan(Forgiving(EgrangStickPresets.Lingkaran)));
            Assert.That(Forgiving(EgrangStickPresets.Lingkaran), Is.GreaterThan(Forgiving(EgrangStickPresets.Segitiga)));
        }

        [Test]
        public void Sweep_GetsFasterAsTheCrossSectionShrinks()
        {
            Assert.That(EgrangStickPresets.Persegi.SweepSeconds,
                        Is.GreaterThan(EgrangStickPresets.Lingkaran.SweepSeconds));
            Assert.That(EgrangStickPresets.Lingkaran.SweepSeconds,
                        Is.GreaterThan(EgrangStickPresets.Segitiga.SweepSeconds));
        }

        [Test]
        public void GreenWindow_IsAtLeastDoubledAtEachStepDown()
        {
            // Ordering alone is not enough: the cards state only measurements, so if the three poles
            // play within a hair of each other the player has no way to tell them apart and the choice
            // is decoration. Two-to-one on the actual aiming window is the floor for "these are
            // different sticks" — the shipped tuning sits near three-to-one.
            float persegi = EgrangStickPresets.Persegi.GreenWindowMilliseconds;
            float lingkaran = EgrangStickPresets.Lingkaran.GreenWindowMilliseconds;
            float segitiga = EgrangStickPresets.Segitiga.GreenWindowMilliseconds;

            Assert.That(persegi, Is.GreaterThanOrEqualTo(lingkaran * 2f),
                        $"square {persegi:0} ms vs round {lingkaran:0} ms is too close to feel different");
            Assert.That(lingkaran, Is.GreaterThanOrEqualTo(segitiga * 2f),
                        $"round {lingkaran:0} ms vs triangular {segitiga:0} ms is too close to feel different");
        }

        [Test]
        public void GreenWindow_StaysHittableOnTheHardestStick()
        {
            // Human reaction jitter on a timed press runs tens of milliseconds. Below about 60 ms the
            // triangle stops being hard and starts being random, which reads as a broken bar.
            Assert.That(EgrangStickPresets.Segitiga.GreenWindowMilliseconds, Is.GreaterThanOrEqualTo(60f));
        }

        [Test]
        public void For_ReturnsThePresetMatchingTheShape()
        {
            foreach (EgrangStickPreset preset in EgrangStickPresets.All)
            {
                Assert.That(EgrangStickPresets.For(preset.Shape).Shape, Is.EqualTo(preset.Shape));
            }
        }
    }
}
