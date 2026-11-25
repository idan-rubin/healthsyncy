import os
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import changes


class SelectionTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.addCleanup(os.chdir, Path.cwd())
        os.chdir(temp.name)
        self.git("init", "-q")
        self.service = Path("src/HealthSyncy.Sync/HealthSyncy.Sync.Domain/UploadService.cs")
        self.service.parent.mkdir(parents=True)
        self.service.write_text("class UploadService {}\n")
        self.deployed = self.commit()

    def git(self, *args):
        return subprocess.check_output(
            ["git", "-c", "user.name=CI", "-c", "user.email=ci@example.org",
             "-c", "commit.gpgsign=false", *args], text=True).strip()

    def commit(self):
        self.git("add", "-A")
        self.git("commit", "-qm", "fixture")
        return self.git("rev-parse", "HEAD")

    def test_move_out_of_service_tree_still_checks_and_ships_the_removal(self):
        Path("docs").mkdir()
        self.service.rename("docs/UploadService.cs")
        self.commit()
        self.assertEqual(changes.select("pull_request"), {"backend": True, "ios": False, "services": True})

    def test_dockerignore_change_checks_backend_on_pull_request(self):
        Path(".dockerignore").write_text("checks/\n")
        self.commit()
        self.assertTrue(changes.select("pull_request")["backend"])

    def test_revert_to_last_successful_tree_still_deploys(self):
        self.service.write_text("class NewUploadService {}\n")
        failed = self.commit()
        self.git("restore", "--source", self.deployed, "--", str(self.service))
        self.commit()
        with patch.object(changes, "last_deployed", return_value=self.deployed):
            self.assertEqual(changes.select("push", failed), {"backend": True, "ios": False, "services": True})

    def test_docs_only_push_skips_when_production_is_current(self):
        Path("README.md").write_text("Documentation\n")
        self.commit()
        expected = {"backend": False, "ios": False, "services": False}
        self.assertEqual(changes.select("pull_request"), expected)
        responses = [[{"id": 1, "sha": self.deployed}], [{"state": "success"}]]
        with patch.dict(os.environ, GITHUB_REPOSITORY="owner/repo"), patch.object(changes, "github", side_effect=responses):
            self.assertEqual(changes.select("push", self.deployed), expected)

    def test_docs_only_push_deploys_outstanding_service_change(self):
        self.service.write_text("class NewUploadService {}\n")
        before = self.commit()
        Path("README.md").write_text("Documentation\n")
        self.commit()
        with patch.object(changes, "last_deployed", return_value=self.deployed):
            self.assertEqual(changes.select("push", before), {"backend": True, "ios": False, "services": True})

    def test_docs_only_push_retries_an_unconfirmed_deployment(self):
        Path("README.md").write_text("Documentation\n")
        self.commit()
        for state in ("failure", "in_progress", "inactive"):
            with self.subTest(state=state), patch.dict(os.environ, GITHUB_REPOSITORY="owner/repo"):
                responses = [[{"id": 2, "sha": self.deployed}], [{"state": state}]]
                with patch.object(changes, "github", side_effect=responses):
                    self.assertTrue(changes.select("push", self.deployed)["services"])


if __name__ == "__main__":
    unittest.main()
