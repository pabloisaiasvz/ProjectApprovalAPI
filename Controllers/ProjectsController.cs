using Microsoft.AspNetCore.Mvc;
using Application.Interfaces.Services;
using Application.DTOs;
using Domain.ValueObjects;
using Domain.Exceptions;

namespace ProjectApprovalAPI.Controllers
{
    [ApiController]
    [Route("Api")]
    public class ProjectController : ControllerBase
    {
        private readonly IProjectManagementService _managementService;
        private readonly IProjectApprovalService _approvalService;
        private readonly IProjectQueryService _queryService;
        private readonly IMasterDataService _masterDataService;

        public ProjectController(
            IProjectManagementService managementService,
            IProjectApprovalService approvalService,
            IProjectQueryService queryService,
            IMasterDataService masterDataService)
        {
            _managementService = managementService;
            _approvalService = approvalService;
            _queryService = queryService;
            _masterDataService = masterDataService;
        }

        // ============================================
        // ----------- Información -----------
        // ============================================

        [HttpGet("Area")]
        [Tags("Información")]
        public async Task<IActionResult> GetAreas() =>
            Ok(await _masterDataService.GetAreasAsync());

        [HttpGet("ProjectType")]
        [Tags("Información")]
        public async Task<IActionResult> GetProjectTypes() =>
            Ok(await _masterDataService.GetProjectTypesAsync());

        [HttpGet("Role")]
        [Tags("Información")]
        public async Task<IActionResult> GetRoles() =>
            Ok(await _masterDataService.GetRolesAsync());

        [HttpGet("ApprovalStatus")]
        [Tags("Información")]
        public async Task<IActionResult> GetApprovalStatuses() =>
            Ok(await _masterDataService.GetApprovalStatusesAsync());

        [HttpGet("User")]
        [Tags("Información")]
        public async Task<IActionResult> GetUsers() =>
            Ok(await _masterDataService.GetUsersAsync());

        // ============================================
        // ----------- Proyectos -----------
        // ============================================

        [HttpPost("/Api/Project")]
        [Tags("Proyectos")]
        public async Task<IActionResult> Create([FromBody] ProjectProposalDto dto)
        {
            try
            {
                var newId = await _managementService.CreateProposalAsync(dto);
                var createdProject = await _queryService.GetByIdAsync(newId);
                return StatusCode(201, createdProject);
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        [HttpGet("/Api/Project")]
        [Tags("Proyectos")]
        public async Task<IActionResult> GetAll([FromQuery] string? title, [FromQuery] string? status, [FromQuery] string? applicant, [FromQuery] string? approvalUser)
        {
            try
            {
                if (!string.IsNullOrEmpty(status) && !int.TryParse(status, out _))
                {
                    return BadRequest(new { message = "El parámetro status debe ser un número válido." });
                }

                if (!string.IsNullOrEmpty(applicant) && !int.TryParse(applicant, out _))
                {
                    return BadRequest(new { message = "El parámetro applicant debe ser un número válido." });
                }

                if (!string.IsNullOrEmpty(approvalUser) && !int.TryParse(approvalUser, out _))
                {
                    return BadRequest(new { message = "El parámetro approvalUser debe ser un número válido." });
                }

                var result = await _queryService.GetAllAsync(title, status, applicant, approvalUser);
                return Ok(result);
            }
            catch (BusinessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("/Api/Project/{id}")]
        [Tags("Proyectos")]
        public async Task<IActionResult> GetById(string id)
        {
            if (!Guid.TryParse(id, out var guidId))
            {
                return BadRequest(new { message = "El ID debe ser un GUID válido." });
            }

            var proposal = await _queryService.GetByIdAsync(guidId);
            if (proposal == null)
                return NotFound(new { message = "Proyecto no encontrado." });

            return Ok(proposal);
        }

        [HttpPatch("/Api/Project/{id}")]
        [Tags("Proyectos")]
        public async Task<IActionResult> Update(string id, [FromBody] ProjectProposalUpdateDto dto)
        {
            try
            {
                if (!Guid.TryParse(id, out var guidId))
                {
                    return BadRequest(new { message = "El ID debe ser un GUID válido." });
                }

                if (string.IsNullOrWhiteSpace(dto.Title))
                    return BadRequest(new { message = "El título es obligatorio." });

                if (string.IsNullOrWhiteSpace(dto.Description))
                    return BadRequest(new { message = "La descripción es obligatoria." });

                if (dto.Duration <= 0)
                    return BadRequest(new { message = "La duración debe ser mayor a cero." });

                await _managementService.UpdateProposalAsync(guidId, dto);

                var updatedProject = await _queryService.GetByIdAsync(guidId);
                return Ok(updatedProject);
            }
            catch (BusinessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (NotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor." });
            }
        }

        [HttpPatch("Project/{id:guid}/decision")]
        [Tags("Proyectos")]
        public async Task<IActionResult> Decision(Guid id, [FromBody] DecisionDto dto)
        {
            try
            {
                if (dto.Id == 0)
                    return BadRequest(new { message = "El ID del paso es obligatorio." });

                if (dto.Status == 0)
                    return BadRequest(new { message = "El status es obligatorio." });

                if (dto.User == 0)
                    return BadRequest(new { message = "El usuario es obligatorio." });

                if (!Enum.IsDefined(typeof(DecisionType), dto.Status))
                    return BadRequest(new { message = "Estado inválido." });

                var decision = (DecisionType)dto.Status;

                bool success = decision switch
                {
                    DecisionType.Approve => await _approvalService.ApproveAsync(id, dto.Id, dto.User),
                    DecisionType.Observe => await _approvalService.ObserveAsync(id, dto.Id, dto.User, dto.Observation ?? ""),
                    DecisionType.Reject => await _approvalService.RejectAsync(id, dto.Id, dto.User),
                    _ => false
                };

                if (!success)
                    return BadRequest(new { message = "No se pudo completar la acción." });

                var updatedProject = await _queryService.GetByIdAsync(id);
                return Ok(updatedProject);
            }
            catch (BusinessException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno del servidor." });
            }
        }
    }
}