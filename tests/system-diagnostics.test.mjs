import test from "node:test";
import assert from "node:assert/strict";

import { detectWindowsHypervisor, normalizeHypervisorDiagnostics, parsePowerShellJson } from "../app/domain/system-diagnostics.js";

test("normalizeHypervisorDiagnostics reports Hyper-V ready hosts", () => {
  const status = normalizeHypervisorDiagnostics({
    platform: "win32",
    hypervisorPresent: true,
    virtualizationFirmwareEnabled: true,
    vmMonitorModeExtensions: true,
    secondLevelAddressTranslationExtensions: true,
  });

  assert.equal(status.available, true);
  assert.equal(status.requiresBiosChange, false);
  assert.equal(status.severity, "ok");
});

test("normalizeHypervisorDiagnostics asks for BIOS virtualization when firmware virtualization is disabled", () => {
  const status = normalizeHypervisorDiagnostics({
    platform: "win32",
    hypervisorPresent: false,
    virtualizationFirmwareEnabled: false,
    vmMonitorModeExtensions: true,
    secondLevelAddressTranslationExtensions: true,
  });

  assert.equal(status.available, false);
  assert.equal(status.requiresBiosChange, true);
  assert.equal(status.severity, "error");
  assert.match(status.message, /BIOS|UEFI/);
});

test("normalizeHypervisorDiagnostics asks for Windows Hyper-V features when firmware virtualization is enabled", () => {
  const status = normalizeHypervisorDiagnostics({
    platform: "win32",
    hypervisorPresent: false,
    virtualizationFirmwareEnabled: true,
    vmMonitorModeExtensions: true,
    secondLevelAddressTranslationExtensions: true,
  });

  assert.equal(status.available, false);
  assert.equal(status.requiresBiosChange, false);
  assert.equal(status.severity, "warning");
  assert.match(status.message, /Hyper-V/);
});

test("parsePowerShellJson accepts compressed ConvertTo-Json output", () => {
  assert.deepEqual(parsePowerShellJson('{"HypervisorPresent":true,"VirtualizationFirmwareEnabled":false}'), {
    HypervisorPresent: true,
    VirtualizationFirmwareEnabled: false,
  });
});

test("detectWindowsHypervisor invokes a valid PowerShell diagnostics script", async () => {
  const calls = [];
  const status = await detectWindowsHypervisor({
    platform: "win32",
    execFileImpl: (_file, args, _options, callback) => {
      calls.push(args);
      callback(null, '{"HypervisorPresent":true,"VirtualizationFirmwareEnabled":true,"VMMonitorModeExtensions":true,"SecondLevelAddressTranslationExtensions":true}', "");
    },
  });

  assert.equal(status.available, true);
  assert.match(calls[0].at(-1), /\[pscustomobject\]@\{\n\s+HypervisorPresent/);
});
