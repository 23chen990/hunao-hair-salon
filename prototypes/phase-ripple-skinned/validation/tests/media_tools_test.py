import importlib.util
import pathlib
import unittest


VALIDATION_ROOT = pathlib.Path(__file__).resolve().parents[1]
ENCODER_PATH = VALIDATION_ROOT / "encode.py"


class MediaToolsTest(unittest.TestCase):
    def load_encoder(self):
        self.assertTrue(ENCODER_PATH.exists(), "encode.py must exist before media encoding can run")
        spec = importlib.util.spec_from_file_location("phase_ripple_encoder", ENCODER_PATH)
        self.assertIsNotNone(spec)
        self.assertIsNotNone(spec.loader)
        encoder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(encoder)
        return encoder

    def test_fourcc_is_decoded_for_reports(self):
        encoder = self.load_encoder()
        value = sum(ord(character) << (8 * index) for index, character in enumerate("avc1"))
        self.assertEqual(encoder.decode_fourcc(value), "avc1")

    def test_segment_bounds_produce_a_six_to_ten_second_clip(self):
        encoder = self.load_encoder()
        output_duration = getattr(encoder, "OUTPUT_DURATION_SECONDS", None)
        self.assertEqual(output_duration, 6.4)
        start, count = encoder.segment_frame_bounds(22.947867708, output_duration, 25.0)
        self.assertEqual(start, 574)
        self.assertEqual(count, 160)
        self.assertGreaterEqual(count / 25.0, 6.0)
        self.assertLessEqual(count / 25.0, 10.0)


if __name__ == "__main__":
    unittest.main()
