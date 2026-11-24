using System.Threading.Tasks;
using PRISM.Core.Models;

namespace PRISM.Core.Interfaces;

public interface IHealthCheckService
{
    Task<HealthCheckReport> RunDiagnosticsAsync();
}
