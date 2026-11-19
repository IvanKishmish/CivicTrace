import { firstEventYear, kindCodeFromLabel, methodCodeFromLabel } from './format.js';

export function normalizeProcurement(raw) {
  const latitude = raw.latitude ?? null;
  const longitude = raw.longitude ?? null;
  const hasCoordinates = Boolean(raw.hasCoordinates ?? (latitude !== null && longitude !== null));

  const procurement = {
    id: raw.id,
    apiId: raw.apiId ?? null,
    planId: raw.planId ?? null,
    title: raw.title ?? '',
    kind: raw.kind ?? '',
    kindCode: kindCodeFromLabel(raw.kind),
    cpvCode: raw.cpvCode ?? '',
    cpvName: raw.cpvName ?? '',
    procurementType: raw.procurementType ?? '',
    methodCode: methodCodeFromLabel(raw.procurementType),
    buyer: raw.buyer ?? '',
    buyerEdrpou: raw.buyerEdrpou ?? '',
    buyerAddress: raw.buyerAddress ?? null,
    contractor: raw.contractor ?? null,
    contractorEdrpou: raw.contractorEdrpou ?? null,
    expectedAmount: raw.expectedAmount ?? null,
    contractAmount: raw.contractAmount ?? null,
    currency: raw.currency ?? 'UAH',
    expectedVatIncluded: raw.expectedVatIncluded ?? null,
    contractVatIncluded: raw.contractVatIncluded ?? null,
    status: raw.status ?? '',
    statusLabel: raw.statusLabel ?? '',
    sourceStatus: raw.sourceStatus ?? '',
    executionStatus: raw.executionStatus ?? 'not_confirmed',
    address: raw.address ?? null,
    placeType: raw.placeType ?? 'unknown',
    latitude,
    longitude,
    hasCoordinates,
    coordinatesStatus: raw.coordinatesStatus ?? null,
    periodStart: raw.periodStart ?? null,
    periodEnd: raw.periodEnd ?? null,
    funding: Array.isArray(raw.funding) ? raw.funding : [],
    nonCompetitiveBasis: raw.nonCompetitiveBasis ?? null,
    bidsListComplete: Boolean(raw.bidsListComplete),
    bids: Array.isArray(raw.bids) ? raw.bids : [],
    events: Array.isArray(raw.events) ? raw.events : [],
    sourceUrl: raw.sourceUrl ?? '',
    notes: Array.isArray(raw.notes) ? raw.notes : [],
    missingData: Array.isArray(raw.missingData) ? raw.missingData : [],
    needsCheck: Array.isArray(raw.needsCheck) ? raw.needsCheck : [],
    verifiedAt: raw.verifiedAt ?? null,
    verifiedBy: raw.verifiedBy ?? null
  };

  procurement.year = firstEventYear(procurement);
  return procurement;
}

export function normalizeMapPoint(raw) {
  return {
    id: raw.id,
    title: raw.title ?? '',
    address: raw.address ?? null,
    placeType: raw.placeType ?? 'unknown',
    latitude: Number(raw.latitude),
    longitude: Number(raw.longitude),
    coordinatesStatus: raw.coordinatesStatus ?? 'needs_check',
    status: raw.status ?? null,
    statusLabel: raw.statusLabel ?? null,
    buyer: raw.buyer ?? null,
    expectedAmount: raw.expectedAmount ?? null,
    contractAmount: raw.contractAmount ?? null,
    currency: raw.currency ?? 'UAH',
    expectedVatIncluded: raw.expectedVatIncluded ?? null,
    contractVatIncluded: raw.contractVatIncluded ?? null,
    cpvCode: raw.cpvCode ?? null
  };
}