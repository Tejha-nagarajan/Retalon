using Retalon.DTOs.Procurement;
using Retalon.Models.Enums;

namespace Retalon.Services.Interfaces;

public interface IProcurementService
{
    Task<List<ProcurementResponseDto>> CreateProcurementAsync(
        Guid userId,
        CreateProcurementRequestDto request,
        CancellationToken cancellationToken = default);

    Task<List<ProcurementResponseDto>> GetProcurementsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<ProcurementResponseDto?> GetProcurementByIdAsync(
        Guid userId,
        long procurementId,
        CancellationToken cancellationToken = default);
    Task<ProcurementResponseDto?> UpdateProcurementStatusAsync(
    long procurementId,
    ProcurementStatus status,
    CancellationToken cancellationToken = default);
}