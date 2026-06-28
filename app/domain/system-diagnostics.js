import { execFile } from "node:child_process";

export function parsePowerShellJson(output = "") {
  const text = String(output || "").trim();
  if (!text) return {};
  return JSON.parse(text);
}

function asBool(value) {
  if (value === true || value === "True" || value === "true" || value === 1 || value === "1") return true;
  if (value === false || value === "False" || value === "false" || value === 0 || value === "0") return false;
  return null;
}

export function normalizeHypervisorDiagnostics(input = {}) {
  const platform = input.platform || process.platform;
  if (platform !== "win32") {
    return {
      platform,
      supported: false,
      available: false,
      requiresBiosChange: false,
      severity: "info",
      message: "Hyper-V診断はWindows環境でのみ確認できます。",
    };
  }

  const hypervisorPresent = asBool(input.hypervisorPresent ?? input.HypervisorPresent);
  const virtualizationFirmwareEnabled = asBool(input.virtualizationFirmwareEnabled ?? input.VirtualizationFirmwareEnabled);
  const vmMonitorModeExtensions = asBool(input.vmMonitorModeExtensions ?? input.VMMonitorModeExtensions);
  const secondLevelAddressTranslationExtensions = asBool(input.secondLevelAddressTranslationExtensions ?? input.SecondLevelAddressTranslationExtensions);
  const cpuSupportsHypervisor = vmMonitorModeExtensions !== false && secondLevelAddressTranslationExtensions !== false;

  if (hypervisorPresent) {
    return {
      platform,
      supported: true,
      available: true,
      hypervisorPresent,
      virtualizationFirmwareEnabled,
      vmMonitorModeExtensions,
      secondLevelAddressTranslationExtensions,
      requiresBiosChange: false,
      severity: "ok",
      message: "Hyper-V/Windows Hypervisorは有効です。Google Play Games開発者エミュレーターを利用できます。",
    };
  }

  if (virtualizationFirmwareEnabled === false) {
    return {
      platform,
      supported: cpuSupportsHypervisor,
      available: false,
      hypervisorPresent,
      virtualizationFirmwareEnabled,
      vmMonitorModeExtensions,
      secondLevelAddressTranslationExtensions,
      requiresBiosChange: true,
      severity: "error",
      message: "BIOS/UEFIでCPU仮想化支援が無効です。Intel VT-xまたはAMD-V/SVMを有効にしてから、WindowsのHyper-V関連機能を有効化してください。",
    };
  }

  return {
    platform,
    supported: cpuSupportsHypervisor,
    available: false,
    hypervisorPresent,
    virtualizationFirmwareEnabled,
    vmMonitorModeExtensions,
    secondLevelAddressTranslationExtensions,
    requiresBiosChange: false,
    severity: "warning",
    message: "CPU仮想化支援は有効ですが、Windows Hypervisorが起動していません。Windowsの機能でHyper-V、仮想マシンプラットフォーム、Windows Hypervisor Platformを有効化してください。",
  };
}

function execFileAsync(file, args, options = {}, execFileImpl = execFile) {
  return new Promise((resolve, reject) => {
    execFileImpl(file, args, { encoding: "utf8", windowsHide: true, timeout: 15000, ...options }, (error, stdout, stderr) => {
      if (error) {
        error.stderr = stderr;
        reject(error);
        return;
      }
      resolve(stdout);
    });
  });
}

export async function detectWindowsHypervisor({ platform = process.platform, execFileImpl = execFile } = {}) {
  if (platform !== "win32") return normalizeHypervisorDiagnostics({ platform });
  const script = [
    "$cs = Get-CimInstance -ClassName Win32_ComputerSystem",
    "$cpu = Get-CimInstance -ClassName Win32_Processor | Select-Object -First 1",
    "[pscustomobject]@{",
    "  HypervisorPresent = $cs.HypervisorPresent",
    "  VirtualizationFirmwareEnabled = $cpu.VirtualizationFirmwareEnabled",
    "  VMMonitorModeExtensions = $cpu.VMMonitorModeExtensions",
    "  SecondLevelAddressTranslationExtensions = $cpu.SecondLevelAddressTranslationExtensions",
    "} | ConvertTo-Json -Compress",
  ].join("\n");
  try {
    const stdout = await execFileAsync("powershell.exe", ["-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script], {}, execFileImpl);
    return normalizeHypervisorDiagnostics({ platform, ...parsePowerShellJson(stdout) });
  } catch (error) {
    return {
      platform,
      supported: true,
      available: false,
      requiresBiosChange: false,
      severity: "warning",
      message: `Hyper-V診断を実行できませんでした: ${error?.message || String(error)}`,
    };
  }
}
