// Copyright ©2026 Scott Blomfield

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Refit;
using RustArchon.Shared.DTOs;

namespace RustArchon.Panel.Clients;

/// <summary>
/// Refit-based API client for <c>DiscountsController</c> - platform-admin management of the discount
/// catalog, gated by the <c>ManageDiscounts</c> permission.
/// </summary>
public interface IDiscountApiClient
{
    [Get("/")]
    Task<List<DiscountDto>> GetAllAsync();

    [Post("/")]
    Task CreateAsync([Body] CreateDiscountRequestDto request);

    [Post("/{id}/set-active")]
    Task SetActiveAsync(Guid id, bool isActive);

    /// <summary>Redeems a code on a specific Organization's behalf.</summary>
    [Post("/redeem")]
    Task<DiscountRedemptionResultDto> RedeemAsync([Body] AdminRedeemDiscountRequestDto request);
}
