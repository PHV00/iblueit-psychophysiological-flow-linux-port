"""Verifica a ligação entre receptor COM P24, players, DeepDDA e Mixer.

Teste estrutural; não compila cenas nem substitui Unity Editor.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2] / "Assets" / "_Game" / "Scripts"


def read(path):
    return (ROOT / path).read_text(encoding="utf-8-sig")


class UnityWiring(unittest.TestCase):
    def test_one_serial_controller_dispatches_both_events(self):
        text = read("Core/Serial/SerialControllerPitaco.cs")
        self.assertIn("OnSerialMessageReceived?.Invoke(", text)
        self.assertIn("OnPitaco24ComboReceived?.Invoke(sample);", text)
        self.assertIn("Pitaco24ComboFrame.TryParse(message, out sample)", text)
        self.assertIn("OnSerialMessageReceived?.Invoke(message);", text)
        self.assertNotIn("BleakClient", text)
        self.assertIn("while (DateTime.UtcNow < echoDeadline)", text)

    def test_platform_and_minigames_accept_combined_bpm(self):
        for path in ("Plataform/Player/Player.cs", "WaterGame/Player.cs",
                     "CakeGame/Player.cs"):
            text = read(path)
            self.assertIn("OnPitaco24ComboReceived +=", text, path)
        for path in ("Plataform/UI/OximeterMeasuresPlataform.cs",
                     "WaterGame/OximeterMeasuresWaterGame.cs",
                     "CakeGame/OximeterMeasuresCakeGame.cs"):
            self.assertIn("SpO2: {spo2}", read(path).replace("SPO2", "SpO2"), path)

    def test_ml_agent_opt_in_only_no_new_rewards(self):
        agent = read("Core/Agents/DeepDDAAgent.cs")
        self.assertIn("includePitaco24BpmObservations = false;", agent)
        self.assertIn("TryGetLatestCombinedBpm(out bpm)", agent)
        self.assertIn("sensor.AddObservation(available ? 1f : 0f)", agent)
        self.assertNotIn("SpO2Value", agent)
        mixer = read("Core/Game/MixerAdaptationGrid.cs")
        self.assertIn("sco != null && sco.IsConnected", mixer)

    def test_legacy_oximeter_is_optional(self):
        self.assertIn("if (sco != null)", read("Plataform/Player/Player.cs"))
        self.assertIn("if (sco != null && sco.IsConnected)", read("WaterGame/Player.cs"))
        self.assertIn("if (sco != null && sco.IsConnected)", read("CakeGame/Player.cs"))
        self.assertIn("if (legacyOximeter != null)", read("Core/Util/Logger/OximetroLogger.cs"))


if __name__ == "__main__":
    unittest.main()
