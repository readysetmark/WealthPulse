
2014-07-19
- Made some renames:
	- Renamed Commodity field on Amount type to Symbol
	- Renamed Value field on Entry type to Commodity
	- Renamed ValueSpecification type to CommodityValue
	- Renamed Value field on ASTEntry type to CommodityValue
	- Renamed CommodityAmountMap type to SymbolAmountMap
	- Renamed Commodity field on CommodityUsage type to Symbol
- Updated functions to account for above renames

TODO: Parsing amount and commodity value should be done together. Create a type to represent options:
	Amount
	Trade