"""
Constants and configuration for Columns CSV enrichment.

This module defines input/output column names and any default values
used by the ColumnsProcessing pipeline. The business logic that maps
`ItemType` to account code/description will be provided later and
implemented in `helpers.py`.
"""

from typing import Dict, List, Tuple

# Input column names (columns to read from source CSV)
INPUT_ITEM_TYPE = "ItemType"
INPUT_ENTITY_HANDLE = "ElementID"
INPUT_ITEM_SOURCE_FILE = "ItemSourceFile"

# Output column names (columns to write to enriched CSV)
ACCOUNT_CODE_COLUMN = "ACCOUNT_CODE"
ACCOUNT_DESCRIPTION_COLUMN = "ACCOUNT_DESCRIPTION"
UOM_COLUMN = "UOM"
MPL_COLUMN = "MPL"
MPL_DESC_COLUMN = "MPL_DESCRIPTION"


# Mapping: Account Description -> Account Code
# account_desc_to_code_map: Dict[str, str] = {
#     "Specialty Walls": "61.06.06",
#     "Anchor Bolts": "81.30.02",
#     "Structural Steel Industrial Structures": "62.03.02",
#     "Module Assembly": "62.03.04",
#     "Stairs": "62.18.04",
#     "Railings": "62.18.12",
#     "Gratings": "62.18.14",
#     "Cable Tray Supports / Cable Supports": "81.06.06",
#     "Electrical Devices": "81.27.02",
#     "Specialty Systems - Plant Communications": "81.33.02",
#     "Specialty Systems - Distributed Antenna System (DAS)": "81.33.04",
#     "Curtain Wall and Glazed Assemblies Subcontracts": "95.03.00.006.04",
#     "Permanent Fences and Gates": "55.12.10",
#     "Concrete Cylinder Piles": "60.03.16",
#     "Caisson - Concrete": "60.09.02",
#     "Grade Beams": "61.03.12",
#     "Topping Concrete": "61.03.14",
#     "Footings": "61.03.10",
#     "Slab on Grade/Mass Slabs": "61.03.06",
# }

# Mapping: Account Code -> UOM
# account_code_to_uom_map: Dict[str, List[str]] = {
#     "55.12.10": ["LM"],
#     "60.03.16": ["M3"],
#     "60.09.02": ["M3"],
#     "61.03.06": ["M3"],
#     "61.03.10": ["M3"],
#     "61.03.12": ["M3"],
#     "61.03.14": ["M3"],
#     "61.06.06": ["Ea"],
#     "61.30.02": ["Ea"],
#     "62.03.02": [
#         "Ton",
#         "Ea"
#     ],
#     "62.03.04": [
#         "Ton",
#         "LM",
#         "Ea"
#     ],
#     "62.18.04": ["LM"],
#     "62.18.12": ["LM"],
#     "62.18.14": ["M2"],
#     "81.06.06": ["Ea"],
#     "81.27.02": ["Ea"],
#     "8Good.33.02": ["Ea"],
#     "81.33.24": ["Ea"],
#     "95.83.08.006.04": ["M2"]   
# }

# Mapping: Account Description -> List of item identifiers
description_to_items: Dict[Tuple[str, str, str], List[str]] = {
    ("Stairs", "62.18.04", "LM"): [
        "ACPPSTRUCTURESTAIR",
        "Assembled Stair"
    ],
    ("Railings", "62.18.12", "LM") : [
        "Railings",
        "Handrail",
        "Top Rail"
    ],
    ("Gratings", "62.18.14", "M2"): [
        "Grate",
        "Grating",
        "Floors: Floor: Metal Floor Assembly ",
        "MTL FLOOR ASSEMBLY"
    ],
    ("Anchor Bolts", "61.30.02", "Ea"): [
        "Anchor Bolt",
        "ANCHOR ROD"
    ],
    ("Cable Tray Supports / Cable Supports", "81.06.06", "Ea"): [
        "Cable Tray Fittings"
    ],
    ("Caisson - Concrete", "60.09.02", "M3"): [
        "Caisson",
        "Caisson1",
        "CAISSON"
    ],
    ("Concrete Cylinder Piles", "60.03.16", "M3"): [
        "ATK_DNNP_PILE",
        "Column-Concrete-Round"
    ],
    ("Curtain Wall and Glazed Assemblies Subcontracts", "95.83.08.006.04", "M2"): [
        "K-Roc"
    ],
    ("Electrical Devices", "81.27.02", "Ea"): [
        "Electrical Fixtures"
    ],
    ("Footings", "61.03.10", "M3"): [
        "Floors: Floor: 400 THK PIER",
        "FOOTING",
        "Footing-Rectangular:",
        "FTG",
        "Floors: Floors 6: Floors 6",
        "Floors: Floor: Generic - SF2"
    ],
    ("Module Assembly", "62.03.04", "LM"): [
        "ACPPSTRUCTURELADDER",
        "ACPPSTRUCTURERAILING",
        "Ladder"
    ],
    ("Module Assembly", "62.03.04", "Ea"): [
        "Generic Models: BASEPLATE",
        "Generic Models: RING: RING",
        "TB- BP UNBRACED",
        "Structural Connections: WF-Column_BP-Steel_Face Based"
    ],    
    ("Permanent Fences and Gates", "55.12.10", "LM"): [
        "Chainlink Fence"
    ],
    ("Specialty Systems - Distributed Antenna System (DAS)", "81.33.24", "Ea"): [
        "Radio_Indoor_Antenna"
    ],
    ("Specialty Systems - Plant Communications", "81.33.02", "Ea"): [
        "Ceiling_Speaker"
    ],
    ("Specialty Walls", "61.06.06", "Ea"): [
        "Generic Models: Corbel: Corbel"
    ],
    ("Structural Steel Industrial Structures", "62.03.02", "Ea"): [
        "Nut",
        "Washer",
        "Screws"
    ],
    ("Topping Concrete", "61.03.14", "M3"): [
        "Topping"
    ],
    ("Structural Steel Industrial Structures", "62.03.02", "Ton"): [
        "Truss Gusset",
        "ACPPSTRUCTUREBEAM",
        "Structural Columns",
        "Structural Framing",
        "Structural Rebar",
        "Rebar",
    ],
    ("Module Assembly", "62.03.04", "Ton"): [
        "ACPPSTRUCTUREPLATE",
        "Gusset",
        "Structural Stiffeners",
        "Structural Connections",
    ],
    ("Grade Beams", "61.03.12", "M3"): [
        "Slab Edges: Slab Edge: Slab Edge 3500X3500",
        "Structural Framing: Precast-Rectangular Beam: 3600x2000x800",
        "Slab Edges: Slab Edge: Slab Edge",
        "Grade Beam",
        "Structural Framing: Beam-Concrete-Rectangular: 1500 x 1000",
        "Structural Foundations: Wall Foundation: STRIP FTG 1000x400 (SF01)",
        "Structural Foundations: Wall Foundation: STRIP FTG 700x300 (SF02)",
        "Structural Foundations: Wall Foundation: STRIP FTG 1300x400 (SF01)",
        "Structural Foundations: Wall Foundation: STRIP FTG 3000x600",
        "Structural Foundations: Wall Foundation: STRIP FTG 3000x600 (SF02)",
        "Walls: Basic Wall: CONC. FDN 350 (FW01)",
        "Walls: Basic Wall: CONC. FDN 600",
        "Walls: Basic Wall: CONC. FDN 700 (FW02)"
    ],
        ("Slab on Grade/Mass Slabs", "61.03.06", "M3"): [
        "Slab on grade",
        "Foundation Slab",
        "Floors: Floor:",
        "Walls: Basic Wall: ISM_Wall"
    ]
}

# MPL map reused from PipesProcessing
mpl_map: Dict[str, str] = {
    "A00": "PROJECT DOCUMENTS (GEH INTERNAL)",
    "A05": "DESIGN TOOL DOCUMENTS",
    "A10": "GENERAL ENGINEERING DOCUMENTS",
    "A11": "PLANT GENERAL REQUIREMENTS DOCUMENTS",
    "A12": "PLANT ARRANGEMENT and 3D MODEL",
    "A13": "ARCHITECTURAL DESIGN",
    "A14": "SITE WORKS , CONSTRUCTION and EXCAVATION",
    "A15": "TECHNICAL DECISIONS AND EVALUATIONS",
    "A16": "TECHNICAL SPECIFICATIONS",
    "A17": "PROBABILISTIC SAFETY ASSESSMENT",
    "A18": "RELIABILITY AVAILABILITY AND MAINTAINABILITY DOCUMENTS",
    "A21": "DETERMINISTIC SAFETY ANALYSES",
    "A22": "APPLICATION ENGINEERING INFORMATION",
    "A23": "RADIATION ENVIRONMENT AND SHIELDING DESIGN",
    "A25": "SEISMIC AND DYNAMIC LOADS",
    "A31": "HUMAN FACTORS ENGINEERING",
    "A32": "INSTRUMENTATION AND CONTROL ENGINEERING",
    "A33": "ELECTRICAL ENGINEERING",
    "A34": "MECHANICAL ENGINEERING",
    "A40": "STRUCTURAL AND CIVIL DESIGN",
    "A50": "COMPOSITE ENGINEERING",
    "A51": "MODULARIZATION",
    "A60": "GENERAL PROCUREMENT DOCUMENTS",
    "A70": "GENERAL QUALITY ASSURANCE AND TESTING DOCUMENTS",
    "A72": "Commissioning Documents",
    "A75": "TRAINING DOCUMENTS",
    "A80": "GENERAL OPERATION AND MAINTENANCE DOCUMENTS",
    "A90": "CERTIFICATION AND LICENSING DOCUMENTS",
    "A93": "SIMULATOR DOCUMENTS",
    "B21": "NUCLEAR BOILER SYSTEM",
    "C10": "Primary Protection System",
    "C11": "Diverse IC Isolation System",
    "C20": "Diverse Protection System",
    "C21": "Gamma Thermometer Data Acquisition System",
    "C22": "FMCRD Motor Control System",
    "C30": "Anticipatory Protection System",
    "C31": "Reactor Control System",
    "C32": "Reactor Auxiliaries Control System",
    "C33": "Equipment Cooling and Environmental Control System",
    "C34": "Electrical Power Supply Control System",
    "C35": "Reactivity Monitoring System",
    "C36": "Plant Data Acquisition, Data Communications, and Normal Operator Interface System",
    "C37": "Control and Monitoring System for DL4b Functions",
    "C38": "Turbine Generator Control System",
    "C39": "Normal Heat Sink and Condensate/FW Control System",
    "C40": "Investment Protection System",
    "C41": "Plant Performance Monitoring",
    "C42": "Plant Environmental Monitoring Interfaces",
    "C43": "Water Chemistry",
    "C44": "Effluent Cleanup Control System",
    "C45": "Network Communications and Operator Interface System",
    "D11": "PROCESS RADIATION AND ENVIRONMENTAL MONITORING SYSTEM",
    "E52": "ISOLATION CONDENSER SYSTEM",
    "F15": "REFUELING AND SERVICING EQUIPMENT SYSTEM",
    "G11": "BORON INJECTION SYSTEM",
    "G12": "CONTROL ROD DRIVE SYSTEM",
    "G20": "Isolation Condenser Pools Cooling and Cleanup System",
    "G22": "SHUTDOWN COOLING SYSTEM",
    "G31": "REACTOR WATER CLEANUP SYSTEM",
    "G41": "FUEL POOL COOLING AND CLEANUP SYSTEM",
    "H10": "SC1 Primary Protection Platform",
    "H11": "SC1 Diverse Protection Platform",
    "H20": "SC2 Diverse Protection Platform",
    "H21": "Gamma Thermometer Data Acquisition Platform",
    "H22": "FMCRD Motor Control Platform",
    "H23": "Emergency Rod Insertion Panels Platform",
    "H24": "SC2 Video Display Platform",
    "H30": "SC3 Primary DCIS Platform",
    "H31": "SC3 Industrial PC Platform",
    "H32": "Turbine Control System Platform",
    "H33": "Rod Control and Information System Platform",
    "H34": "SC3 Video Display Platform",
    "H35": "Core Monitoring Platform",
    "H40": "SCN Primary DCIS Platform",
    "H41": "Condition Monitoring Platform",
    "H42": "Cyber Security Intrusion Detection and Monitoring Platform",
    "H91": "Control Rooms",
    "H92": "Emergency and Outage Support Facilities",
    "H93": "SIMULATOR",
    "H94": "Computer-Based Procedure System",
    "J11": "CORE AND FUEL",
    "K10": "LIQUID WASTE MANAGEMENT SYSTEM",
    "K20": "SOLID WASTE MANAGEMENT SYSTEM",
    "K30": "OFFGAS SYSTEM",
    "N21": "CONDENSATE AND FEEDWATER HEATING SYSTEM",
    "N25": "CONDENSATE FILTERS AND DEMINERALIZERS SYSTEM",
    "N31": "MAIN TURBINE EQUIPMENT",
    "N35": "MOISTURE SEPARATOR REHEATER SYSTEM",
    "N37": "TURBINE BYPASS SYSTEM",
    "N41": "GENERATOR AND EXCITER",
    "N61": "MAIN CONDENSER AND AUXILIARIES",
    "N71": "CIRCULATING WATER SYSTEM",
    "P25": "CHILLED WATER EQUIPMENT",
    "P40": "PLANT COOLING WATER SYSTEM",
    "P52": "PLANT PNEUMATICS SYSTEM",
    "P73": "Hydrogen Water Chemistry System",
    "P85": "Zinc Injection Passivation System",
    "P87": "On-Line NobleChemTM System",
    "R10": "EMERGENCY POWER BACKUP DC AND UPS ELECTRICAL SYSTEM",
    "R15": "LIGHTING AND SERVICE POWER SYSTEM",
    "R20": "STANDBY POWER SYSTEM",
    "R30": "PREFERRED POWER SYSTEM",
    "R31": "Cable and Raceway System",
    "R41": "Grounding and Lightning Protection System",
    "R51": "Non-DCIS Communication System",
    "R61": "Freeze and Cathodic Protection System",
    "S21": "SWITCHYARD",
    "T10": "PRIMARY CONTAINMENT SYSTEM",
    "T31": "CONTAINMENT INERTING SYSTEM",
    "T41": "CONTAINMENT COOLING SYSTEM",
    "U31": "CRANES, HOISTS AND ELEVATORS",
    "U41": "HEATING VENTILATION AND COOLING SYSTEM",
    "U43": "FIRE PROTECTION SYSTEM",
    "U50": "EQUIPMENT AND FLOOR DRAIN SYSTEM",
    "U71": "REACTOR BUILDING STRUCTURE",
    "U72": "TURBINE BUILDING STRUCTURE",
    "U73": "CONTROL BUILDING STRUCTURE",
    "U74": "RADWASTE STRUCTURE",
    "U75": "SERVICE BUILDING STRUCTURE",
    "U87": "PUMPHOUSE STRUCTURE",
    "W24": "NORMAL HEAT SINK",
    "Y53": "WATER, GAS, AND CHEMICAL PADS",
    "Y81": "SPENT FUEL MANAGEMENT",
    "Y86": "SECURITY",
    "Y99": "YARD/BOP",
}
