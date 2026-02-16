-- Seed popularity scores for major airports
-- Run this after adding the popularity_score column

-- TIER 1: Global Super Hubs (Score: 1000)
UPDATE airports 
SET popularity_score = 1000 
WHERE airport_iata IN (
    'JFK', 'LAX', 'ORD', 'DFW', 'DEN',
    'ATL', 'LHR', 'CDG', 'AMS', 'FRA',
    'IST', 'DXB', 'SIN', 'HND', 'NRT',
    'ICN', 'PVG', 'HKG', 'SYD'
);

-- TIER 2: Major Regional Hubs (Score: 850)
UPDATE airports 
SET popularity_score = 850 
WHERE airport_iata IN (
    'YYZ', 'YVR', 'MEX', 'SFO', 'SEA',
    'MIA', 'BOS', 'LGA', 'LIH', 'PHX',
    'FCO', 'MAD', 'BCN', 'DUB', 'LGW',
    'MAN', 'ORY', 'BVA', 'KIX', 'PEK'
);

-- TIER 3: Large Regional Airports (Score: 600)
UPDATE airports 
SET popularity_score = 600 
WHERE airport_iata IN (
    'YUL', 'YEG', 'YYC', 'YOW', 'YTZ',
    'BNA', 'IAD', 'DCA', 'PHL', 'TPA',
    'FLL', 'MCO', 'AUS', 'IAH', 'SAT',
    'SAN', 'LAS', 'MSP', 'STL', 'DTW',
    'PIT', 'CVG', 'EWR', 'MSY', 'SLC'
);

-- TIER 4: Regional Airports (Score: 300)
UPDATE airports 
SET popularity_score = 300 
WHERE airport_iata IN (
    'YKA', 'YLD', 'TYS', 'GRR', 'CLE',
    'IND', 'MCI', 'TUS', 'ABQ', 'SMF',
    'SJC', 'OAK', 'EUG', 'PDX', 'OLM',
    'KTN', 'JNU', 'ANC', 'FAI', 'PWM',
    'BTV'
);

-- TIER 5: Small Regional/Local (Score: 100)
UPDATE airports 
SET popularity_score = 100 
WHERE airport_iata IN (
    'YJA', 'YPA', 'YEW', 'YLB', 'YFB',
    'ACV', 'EYW'
);

-- Verify the update
SELECT 
    popularity_score,
    COUNT(*) as airport_count
FROM airports
GROUP BY popularity_score
ORDER BY popularity_score DESC;

-- Show sample of updated airports
SELECT 
    airport_iata,
    airport_name,
    city_name,
    country_name,
    popularity_score
FROM airports
WHERE popularity_score > 10
ORDER BY popularity_score DESC, airport_name
LIMIT 20;
