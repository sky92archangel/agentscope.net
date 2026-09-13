// Copyright 2024-2026 the original author or authors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System.Diagnostics;

namespace AgentScope.Extensions.Sandbox.Kubernetes.Connector;

/// <summary>
/// 沙箱连接策略接口。4 种方式：Direct / InCluster / Gateway / PortForward。
/// 对应 Java: io.agentscope.sandbox.kubernetes.connector.SandboxConnectorStrategy
/// </summary>
public interface ISandboxConnectorStrategy
{
    /// <summary>获取沙箱的连接字符串（如 http://pod-ip:8888）。</summary>
    Task<string> GetConnectionStringAsync(string sandboxName, string @namespace);
}

/// <summary>
/// 直连策略：通过 Pod IP + 端口直连沙箱。
/// </summary>
public class DirectConnectorStrategy : ISandboxConnectorStrategy
{
    private readonly string _kubeConfigPath;
    private readonly int _port;

    public DirectConnectorStrategy(string? kubeConfigPath = null, int port = 8888)
    {
        _kubeConfigPath = kubeConfigPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config");
        _port = port;
    }

    public async Task<string> GetConnectionStringAsync(string sandboxName, string @namespace)
    {
        var ip = await GetPodIpAsync(sandboxName, @namespace).ConfigureAwait(false);
        return $"http://{ip}:{_port}";
    }

    private Task<string> GetPodIpAsync(string podName, string @namespace)
    {
        var (exit, stdout, _) = RunKubectl(
            $"get pod {podName} -n {@namespace} -o jsonpath='{{.status.podIP}}'");
        if (exit != 0 || string.IsNullOrWhiteSpace(stdout))
            throw new InvalidOperationException(
                $"无法获取 Pod {podName} 的 IP 地址");
        return Task.FromResult(stdout.Trim().Trim('\''));
    }

    private (int ExitCode, string StdOut, string StdErr) RunKubectl(string args)
    {
        var psi = new ProcessStartInfo("kubectl", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment = { ["KUBECONFIG"] = _kubeConfigPath }
        };
        using var proc = Process.Start(psi);
        if (proc == null) return (-1, "", "无法启动 kubectl");
        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        return (proc.ExitCode, stdout, stderr);
    }
}

/// <summary>
/// 端口转发策略：通过 kubectl port-forward 建立本地隧道连接沙箱。
/// </summary>
public class PortForwardConnectorStrategy : ISandboxConnectorStrategy
{
    private readonly string _kubeConfigPath;
    private readonly int _localPort;
    private Process? _forwardProcess;

    public PortForwardConnectorStrategy(string? kubeConfigPath = null, int localPort = 8888)
    {
        _kubeConfigPath = kubeConfigPath
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".kube", "config");
        _localPort = localPort;
    }

    public Task<string> GetConnectionStringAsync(string sandboxName, string @namespace)
    {
        // 启动 kubectl port-forward 后台进程
        var psi = new ProcessStartInfo("kubectl",
            $"port-forward pod/{sandboxName} {_localPort}:8888 -n {@namespace}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment = { ["KUBECONFIG"] = _kubeConfigPath },
            CreateNoWindow = true,
            UseShellExecute = false,
        };

        _forwardProcess = new Process { StartInfo = psi };
        _forwardProcess.Start();

        return Task.FromResult($"http://localhost:{_localPort}");
    }

    /// <summary>停止端口转发进程。</summary>
    public void Stop() => _forwardProcess?.Kill(entireProcessTree: true);
}
