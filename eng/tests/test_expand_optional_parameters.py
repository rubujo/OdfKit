import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_PATH = Path(__file__).resolve().parents[1] / "Expand-OptionalParameters.py"
SPEC = importlib.util.spec_from_file_location("expand_optional_parameters", SCRIPT_PATH)
assert SPEC is not None and SPEC.loader is not None
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class ExpandOptionalParametersTests(unittest.TestCase):
    def test_expands_generic_methods_with_and_without_constraints(self) -> None:
        source = """namespace Example;

public sealed class Sample
{
    public T Convert<T>(string value, object? options = null)
    {
        return default!;
    }

    public T Create<T>(string value, object? options = null)
        where T : new()
    {
        return new T();
    }
}
"""
        with tempfile.TemporaryDirectory() as temp_dir:
            path = Path(temp_dir) / "Sample.cs"
            path.write_text(source, encoding="utf-8")

            expanded = MODULE.process_file(path, dry_run=False)
            result = path.read_text(encoding="utf-8")

        self.assertEqual(2, expanded)
        self.assertIn("public T Convert<T>(string value) => Convert<T>(value, null);", result)
        self.assertIn("public T Convert<T>(string value, object? options)", result)
        self.assertIn("public T Create<T>(string value) where T : new() => Create<T>(value, null);", result)
        self.assertIn("public T Create<T>(string value, object? options) where T : new()", result)


if __name__ == "__main__":
    unittest.main()
