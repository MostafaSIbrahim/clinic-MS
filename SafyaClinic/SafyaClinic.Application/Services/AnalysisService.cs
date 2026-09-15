using SafyaClinic.Application.DTOs.Analysis;
using SafyaClinic.Application.DTOs.Common;
using SafyaClinic.Application.DTOs.MedicalRecord;
using SafyaClinic.Application.Interfaces.Services;
using SafyaClinic.Domain.Entities.Analysis;
using SafyaClinic.Domain.Enums;
using SafyaClinic.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace SafyaClinic.Application.Services;

public class AnalysisService : IAnalysisService
{
    private readonly IUnitOfWork _uow;

    private static readonly Expression<Func<MedicalAnalysis, MedicalAnalysisDto>>
    AnalysisDtoProjection = a => new MedicalAnalysisDto
    {
        Id = a.Id,
        PatientId = a.PatientId,
        PatientName = $"{a.Patient.FirstName} {a.Patient.LastName}",
        DoctorId = a.DoctorId,
        DoctorName = a.Doctor.FullName,
        RecordId = a.RecordId,
        AnalysisTypeId = a.AnalysisTypeId,
        AnalysisTypeName = a.Type.TypeName,
        PreparationInstructions = a.Type.PreparationInstructions,
        Status = a.Status.ToString(),
        IsUrgent = a.IsUrgent,
        RequestDate = a.RequestDate,
        ResultDate = a.ResultDate,
        ResultNotes = a.ResultNotes,
        Attachments = a.Attachments.Select(att => new AttachmentDto
        {
            Id = att.Id,
            FileName = att.FileName,
            FilePath = att.FilePath,
            ContentType = att.ContentType,
            FileSizeBytes = (long)att.FileSizeBytes,
            UploadedAt = att.UploadedAt
        })
    };

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

        var distinctTypeIds = request.AnalysisTypeIds
            .Distinct()
            .ToList();

        var existingTypeIds = (await _uow.AnalysisTypes
            .Query()
            .Where(t => distinctTypeIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToListAsync())
            .ToHashSet();

        foreach (var typeId in distinctTypeIds)
        {
            if (!existingTypeIds.Contains(typeId))
                return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Failure(
                    $"Analysis type ID {typeId} not found.");
        }

        var created = distinctTypeIds
            .Select(typeId => new MedicalAnalysis
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
            })
            .ToList();

        await _uow.MedicalAnalyses.AddRangeAsync(created);

        await _uow.SaveChangesAsync();

        var createdIds = created
            .Select(a => a.Id)
            .ToList();

        var dtos = await _uow.MedicalAnalyses
            .Query()
            .Where(a => createdIds.Contains(a.Id))
            .OrderBy(a => a.Id)
            .Select(AnalysisDtoProjection)
            .ToListAsync();

        return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Success(dtos);
    }

    public async Task<ServiceResult<MedicalAnalysisDto>> GetAnalysisByIdAsync(
     int analysisId)
    {
        var analysis = await _uow.MedicalAnalyses.Query()
            .Where(a => a.Id == analysisId)
            .Select(AnalysisDtoProjection)
            .FirstOrDefaultAsync();

        if (analysis is null)
            return ServiceResult<MedicalAnalysisDto>.Failure("Analysis not found.");

        return ServiceResult<MedicalAnalysisDto>.Success(analysis);
    }

    public async Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> GetPatientAnalysesAsync(
     int patientId)
    {
        var analyses = await _uow.MedicalAnalyses.Query()
            .Where(a => a.PatientId == patientId)
            .OrderByDescending(a => a.RequestDate)
            .Select(AnalysisDtoProjection)
            .ToListAsync();

        return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Success(analyses);
    }

    public async Task<ServiceResult<IEnumerable<MedicalAnalysisDto>>> GetAnalysesByRecordAsync(
     int recordId)
    {
        var analyses = await _uow.MedicalAnalyses.Query()
            .Where(a => a.RecordId == recordId)
            .OrderByDescending(a => a.RequestDate)
            .Select(AnalysisDtoProjection)
            .ToListAsync();

        return ServiceResult<IEnumerable<MedicalAnalysisDto>>.Success(analyses);
    }

    public async Task<ServiceResult<PagedResult<MedicalAnalysisDto>>> SearchAnalysesAsync(
     PaginationRequest request, string? status = null)
    {
        var query = _uow.MedicalAnalyses.Query();

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<AnalysisStatus>(status, out var statusFilter))
        {
            query = query.Where(a => a.Status == statusFilter);
        }

        var search = request.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(a =>
                $"{a.Patient.FirstName} {a.Patient.LastName}".Contains(search) ||
                a.Type.TypeName.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var analyses = await query
            .OrderByDescending(a => a.RequestDate)
            .ThenByDescending(a => a.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(AnalysisDtoProjection)
            .ToListAsync();

        return ServiceResult<PagedResult<MedicalAnalysisDto>>.Success(
            new PagedResult<MedicalAnalysisDto>
            {
                Items = analyses,
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
        var types = await _uow.AnalysisTypes.Query()
            .OrderBy(t => t.TypeName)
            .Select(t => new AnalysisTypeDto
            {
                Id = t.Id,
                TypeName = t.TypeName,
                Description = t.Description,
                DefaultCost = t.DefaultCost,
                PreparationInstructions = t.PreparationInstructions
            })
            .ToListAsync();

        return ServiceResult<IEnumerable<AnalysisTypeDto>>.Success(types);
    }

    // ── Mapper ────────────────────────────────────────────────

    private async Task<MedicalAnalysisDto> BuildAnalysisDtoAsync(MedicalAnalysis a)
    {
        var dto = await _uow.MedicalAnalyses
            .Query()
            .Where(x => x.Id == a.Id)
            .Select(AnalysisDtoProjection)
            .FirstOrDefaultAsync();

        if (dto is not null)
            return dto;

        // Preserve the existing mapper's behavior if the analysis
        // cannot be reloaded for some unexpected reason.
        return new MedicalAnalysisDto
        {
            Id = a.Id,
            PatientId = a.PatientId,
            PatientName = "",
            DoctorId = a.DoctorId,
            DoctorName = "",
            RecordId = a.RecordId,
            AnalysisTypeId = a.AnalysisTypeId,
            AnalysisTypeName = "",
            PreparationInstructions = null,
            Status = a.Status.ToString(),
            IsUrgent = a.IsUrgent,
            RequestDate = a.RequestDate,
            ResultDate = a.ResultDate,
            ResultNotes = a.ResultNotes,
            Attachments = Enumerable.Empty<AttachmentDto>()
        };
    }
}