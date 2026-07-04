using SafyaClinic.Application.DTOs.Analysis;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;

namespace SafyaClinic.Application.Services;

public class AnalysisService : IAnalysisService
{
    private readonly IUnitOfWork _uow;

    public AnalysisService(IUnitOfWork uow) => _uow = uow;

    public async Task<ServiceResult<MedicalAnalysisDto>> RequestAnalysisAsync(
        RequestAnalysisRequest request, int requestedBy)
    {
        if (!await _uow.Patients.ExistsAsync(request.PatientId))
            return ServiceResult<MedicalAnalysisDto>.Failure("Patient not found.");
        if (!await _uow.Users.ExistsAsync(request.DoctorId))
            return ServiceResult<MedicalAnalysisDto>.Failure("Doctor not found.");
        if (!await _uow.AnalysisTypes.ExistsAsync(request.AnalysisTypeId))
            return ServiceResult<MedicalAnalysisDto>.Failure("Analysis type not found.");

        var analysis = new MedicalAnalysis
        {
            PatientId = request.PatientId,
            DoctorId = request.DoctorId,
            RecordId = request.RecordId,
            AnalysisTypeId = request.AnalysisTypeId,
            Status = AnalysisStatus.Requested,
            RequestDate = DateTime.UtcNow,
            IsUrgent = request.IsUrgent,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = requestedBy
        };

        await _uow.MedicalAnalyses.AddAsync(analysis);
        await _uow.SaveChangesAsync();
        return ServiceResult<MedicalAnalysisDto>.Success(await BuildAnalysisDtoAsync(analysis));
    }

    public async Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> RequestAnalysesAsync(
        RequestAnalysisBatchRequest request, int requestedBy)
    {
        if (request.AnalysisTypeIds is null || !request.AnalysisTypeIds.Any())
            return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Failure(
                "Select at least one analysis type.");

        if (!await _uow.Patients.ExistsAsync(request.PatientId))
            return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Failure("Patient not found.");
        if (!await _uow.Users.ExistsAsync(request.DoctorId))
            return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Failure("Doctor not found.");

        var distinctTypeIds = request.AnalysisTypeIds.Distinct().ToList();
        var created = new List<MedicalAnalysis>();

        foreach (var typeId in distinctTypeIds)
        {
            if (!await _uow.AnalysisTypes.ExistsAsync(typeId))
                return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Failure(
                    $"Analysis type ID {typeId} not found.");

            var analysis = new MedicalAnalysis
            {
                PatientId = request.PatientId,
                DoctorId = request.DoctorId,
                RecordId = request.RecordId,
                AnalysisTypeId = typeId,
                Status = AnalysisStatus.Requested,
                RequestDate = DateTime.UtcNow,
                IsUrgent = request.IsUrgent,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = requestedBy
            };
            await _uow.MedicalAnalyses.AddAsync(analysis);
            created.Add(analysis);
        }

        await _uow.SaveChangesAsync();

        var dtos = new List<MedicalAnalysisDto>();
        foreach (var a in created)
            dtos.Add(await BuildAnalysisDtoAsync(a));

        return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Success(dtos);
    }

    public async Task<ServiceResult<MedicalAnalysisDto>> GetAnalysisByIdAsync(int analysisId)
    {
        var analysis = await _uow.MedicalAnalyses.GetByIdAsync(analysisId);
        if (analysis is null)
            return ServiceResult<MedicalAnalysisDto>.Failure("Analysis not found.");
        return ServiceResult<MedicalAnalysisDto>.Success(await BuildAnalysisDtoAsync(analysis));
    }

    public async Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> GetPatientAnalysesAsync(
        int patientId)
    {
        var analyses = await _uow.MedicalAnalyses.FindAsync(a => a.PatientId == patientId);
        var dtos = new List<MedicalAnalysisDto>();
        foreach (var a in analyses.OrderByDescending(a => a.RequestDate))
            dtos.Add(await BuildAnalysisDtoAsync(a));
        return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Success(dtos);
    }

    public async Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> GetAnalysesByRecordAsync(
        int recordId)
    {
        var analyses = await _uow.MedicalAnalyses.FindAsync(a => a.RecordId == recordId);
        var dtos = new List<MedicalAnalysisDto>();
        foreach (var a in analyses.OrderByDescending(a => a.RequestDate))
            dtos.Add(await BuildAnalysisDtoAsync(a));
        return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Success(dtos);
    }

    public async Task<ServiceResult<PagedResult<MedicalAnalysisDto>>> SearchAnalysesAsync(
        PaginationRequest request, string? status = null)
    {
        var analyses = (await _uow.MedicalAnalyses.GetAllAsync()).AsEnumerable();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AnalysisStatus>(status, out var statusFilter))
        {
            analyses = analyses.Where(a => a.Status == statusFilter);
        }

        // Build DTOs first (need patient name to search on), then filter/page.
        var all = new List<MedicalAnalysisDto>();
        foreach (var a in analyses.OrderByDescending(a => a.RequestDate))
            all.Add(await BuildAnalysisDtoAsync(a));

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            all = all.Where(a =>
                a.PatientName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                a.AnalysisTypeName.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var totalCount = all.Count;
        var page = all.Skip((request.Page - 1) * request.PageSize)
                       .Take(request.PageSize)
                       .ToList();

        return ServiceResult<PagedResult<MedicalAnalysisDto>>.Success(new PagedResult<MedicalAnalysisDto>
        {
            Items = page,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        });
    }

    public async Task<ServiceResult<AttachmentDto>> GetAttachmentAsync(int attachmentId)
    {
        var a = await _uow.AnalysisAttachments.GetByIdAsync(attachmentId);
        if (a is null) return ServiceResult<AttachmentDto>.Failure("Attachment not found.");

        return ServiceResult<AttachmentDto>.Success(new AttachmentDto
        {
            Id = a.Id,
            FileName = a.FileName,
            FilePath = a.FilePath,
            ContentType = a.ContentType,
            FileSizeBytes = (long)a.FileSizeBytes,
            UploadedAt = a.UploadedAt
        });
    }

    public async Task<ServiceResult> UpdateStatusAsync(
        int analysisId, UpdateAnalysisStatusRequest request)
    {
        var analysis = await _uow.MedicalAnalyses.GetByIdAsync(analysisId);
        if (analysis is null) return ServiceResult.Failure("Analysis not found.");
        if (!Enum.TryParse<AnalysisStatus>(request.Status, out var status))
            return ServiceResult.Failure("Invalid status value.");

        analysis.Status = status;
        analysis.ResultDate = request.ResultDate;
        analysis.ResultNotes = request.ResultNotes?.Trim();
        analysis.UpdatedAt = DateTime.UtcNow;

        _uow.MedicalAnalyses.Update(analysis);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Analysis status updated.");
    }

    public async Task<ServiceResult> AddAttachmentAsync(
        int analysisId, string filePath, string fileName,
        string contentType, long fileSize, int uploadedBy)
    {
        if (!await _uow.MedicalAnalyses.ExistsAsync(analysisId))
            return ServiceResult.Failure("Analysis not found.");

        await _uow.AnalysisAttachments.AddAsync(new AnalysisAttachment
        {
            AnalysisId = analysisId,
            FileName = fileName,
            FilePath = filePath,
            ContentType = contentType,
            FileSizeBytes = fileSize,
            UploadedAt = DateTime.UtcNow,
            UploadedBy = uploadedBy
        });
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Attachment added.");
    }

    public async Task<ServiceResult> DeleteAttachmentAsync(int attachmentId)
    {
        var a = await _uow.AnalysisAttachments.GetByIdAsync(attachmentId);
        if (a is null) return ServiceResult.Failure("Attachment not found.");

        _uow.AnalysisAttachments.Delete(a);
        await _uow.SaveChangesAsync();
        return ServiceResult.Success("Attachment deleted.");
    }

    public async Task<ServiceResult<IEnumerable<AnalysisTypeDto>>> GetAnalysisTypesAsync()
    {
        var types = await _uow.AnalysisTypes.GetAllAsync();
        return ServiceResult<IEnumerable<AnalysisTypeDto>>.Success(
            types.Select(t => new AnalysisTypeDto
            {
                Id = t.Id,
                TypeName = t.TypeName,
                Description = t.Description,
                DefaultCost = t.DefaultCost,
                PreparationInstructions = t.PreparationInstructions
            }));
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<MedicalAnalysisDto> BuildAnalysisDtoAsync(MedicalAnalysis a)
    {
        var patient = await _uow.Patients.GetByIdAsync(a.PatientId);
        var doctor = await _uow.Users.GetByIdAsync(a.DoctorId);
        var aType = await _uow.AnalysisTypes.GetByIdAsync(a.AnalysisTypeId);
        var attachments = await _uow.AnalysisAttachments.FindAsync(att => att.AnalysisId == a.Id);

        return new MedicalAnalysisDto
        {
            Id = a.Id,
            PatientId = a.PatientId,
            PatientName = patient is null ? "" : $"{patient.FirstName} {patient.LastName}",
            DoctorId = a.DoctorId,
            DoctorName = doctor?.FullName ?? "",
            RecordId = a.RecordId,
            AnalysisTypeId = a.AnalysisTypeId,
            AnalysisTypeName = aType?.TypeName ?? "",
            PreparationInstructions = aType?.PreparationInstructions,
            Status = a.Status.ToString(),
            IsUrgent = a.IsUrgent,
            RequestDate = a.RequestDate,
            ResultDate = a.ResultDate,
            ResultNotes = a.ResultNotes,
            Attachments = attachments.Select(att => new AttachmentDto
            {
                Id = att.Id,
                FileName = att.FileName,
                FilePath = att.FilePath,
                ContentType = att.ContentType,
                FileSizeBytes = (long)att.FileSizeBytes,
                UploadedAt = att.UploadedAt
            })
        };
    }
}