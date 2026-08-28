import os
import sys
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
import json
import csv
import math
from numpy import arange
from PIL import Image, ImageTk
from OCC.Core.BRepPrimAPI import BRepPrimAPI_MakeCylinder
from OCC.Core.BRepBuilderAPI import BRepBuilderAPI_Transform
from OCC.Core.gp import gp_Ax2, gp_Pnt, gp_Dir, gp_Trsf, gp_Vec, gp_Ax1
from OCC.Core.TopoDS import TopoDS_Compound, TopoDS_Shape
from OCC.Core.BRep import BRep_Builder
from OCC.Core.STEPControl import STEPControl_Writer, STEPControl_AsIs
from OCC.Core.Interface import Interface_Static
from OCC.Core.TCollection import TCollection_HAsciiString
from OCC.Core.TNaming import TNaming_Builder
from OCC.Core.TDF import TDF_Label
from OCC.Core.XCAFDoc import XCAFDoc_DocumentTool
from OCC.Core.TDocStd import TDocStd_Document
from OCC.Core.XCAFApp import XCAFApp_Application

# Color scheme
PRIMARY_COLOR = "#A02128"  # Dark Red
SECONDARY_COLOR = "#CCCCCC"  # Light Gray
BG_COLOR = "#666666"  # Dark Gray
TEXT_COLOR = "#FFFFFF"  # White
ACCENT_COLOR = "#000000"  # Black

class StudPlateGenerator:
    def __init__(self, master):
        self.master = master
        self.master.title("Stud Plate Generator")
        self.master.geometry("800x800")

        self.setup_styles()
        self.inputs = {}
        self.create_widgets()
        self.create_image_frame()

    def setup_styles(self):
        style = ttk.Style()
        style.theme_create("BOSTheme", parent="alt", settings={
            "TFrame": {"configure": {"background": BG_COLOR, "foreground": TEXT_COLOR}},
            "TLabel": {"configure": {"background": BG_COLOR, "foreground": TEXT_COLOR}},
            "TButton": {"configure": {"background": PRIMARY_COLOR, "foreground": TEXT_COLOR, "Relief": "RAISED"}},
            "TEntry": {"configure": {"fieldbackground": BG_COLOR, "foreground": TEXT_COLOR}},
            "TCombobox": {"configure": {"selectbackground": BG_COLOR, "fieldbackground": BG_COLOR, "foreground": TEXT_COLOR}},
            "Horizontal.TProgressbar": {"configure": {"background": PRIMARY_COLOR}},
        })
        style.theme_use("BOSTheme")

    def create_widgets(self):
        self.main_frame = ttk.Frame(self.master)        
        self.main_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)

        # Load JSON button
        ttk.Button(self.main_frame, text="Load Saved Inputs", command=self.load_json_file).grid(row=0, column=0, padx=5, pady=5, sticky="w")

        # Common input fields
        self.common_frame = ttk.Frame(self.main_frame)
        self.common_frame.grid(row=1, column=0, columnspan=2, padx=0, pady=0, sticky="w")

        # Plate Type dropdown
        ttk.Label(self.common_frame, text="Plate Type:").grid(row=0, column=0, padx=5, pady=5, sticky="w")
        self.plate_type = ttk.Combobox(self.common_frame, width=25, values=[
            "Rectangular Flat", "Rectangular Curved Inner", "Rectangular Curved Outer"            
        ])
        #"Common Floor", "Basemat" removed YTD

        self.plate_type.grid(row=0, column=1, padx=5, pady=5, sticky="w")
        self.plate_type.bind("<<ComboboxSelected>>", self.update_form)

        self.create_common_inputs()

        # Dynamic input fields container
        self.dynamic_frame = ttk.Frame(self.main_frame)
        self.dynamic_frame.grid(row=11, column=0, columnspan=2, padx=0, pady=0, sticky="w")

        # File location input and browse button
        self.file_location_frame = ttk.Frame(self.main_frame)
        self.file_location_frame.grid(row=100, column=0, columnspan=2, padx=0, pady=0, sticky="w")

        ttk.Label(self.file_location_frame, text="Save Location:").grid(row=0, column=0, padx=5, pady=5, sticky="w")
        self.save_location = tk.StringVar()
        ttk.Entry(self.file_location_frame, textvariable=self.save_location, width=50).grid(row=0, column=1, padx=5, pady=5, sticky="w")
        ttk.Button(self.file_location_frame, text="Browse", command=self.browse_save_location).grid(row=0, column=2, padx=5, pady=5)

        # Generate button
        ttk.Button(self.main_frame, text="Generate", command=self.generate_files).grid(row=101, column=0, padx=5, pady=20)

        # Omission rules section                
        ttk.Label(self.main_frame, text="Omission Rules:").grid(row=98, column=0, padx=5, pady=5, sticky="w")
        ttk.Button(self.main_frame, text="Add Omission Rule", command=self.add_omission_rule).grid(row=98, column=1, padx=5, pady=5)
        self.omission_frame = ttk.Frame(self.main_frame)
        self.omission_frame.grid(row=99, column=0, columnspan=2, padx=5, pady=5, sticky="w")
        

        self.omission_rules = []        

    def create_common_inputs(self):
        common_inputs = [
            ("Part Number", "", "", ""),
            ("Part Description", "", "", ""),
            ("Datum Thickness", " [T]", "mm", ""),
            ("Stud Length", " [S]", "", ["6 inches", "9 inches"]),
            ("Datum Alignment", "", "", ["Diaphragm", "Studs"]),
            ("Stud Start Height", " [C]", "mm", ""),
            ("Stud Spacing", " [B]", "mm", ""),
        ]

        for i, (label, tag, unit, options) in enumerate(common_inputs, start=1):
            ttk.Label(self.common_frame, width=25, text=f"{label}{tag}:").grid(row=i+1, column=0, padx=5, pady=5, sticky="w")
            if options:
                self.inputs[label] = ttk.Combobox(self.common_frame, width=25, values=options)
            else:
                self.inputs[label] = ttk.Entry(self.common_frame, width=27)
            self.inputs[label].grid(row=i+1, column=1, padx=5, pady=5, sticky="w")
            if unit:
                ttk.Label(self.common_frame, text=unit).grid(row=i+1, column=2, padx=5, pady=5, sticky="w")

    def update_form(self, event=None):

        # Identify which inputs belong to the dynamic frame
        dynamic_inputs_to_remove = []
        for key, widget in self.inputs.items():
            if widget.winfo_parent() == str(self.dynamic_frame):
                dynamic_inputs_to_remove.append(key)
        
        # Remove the identified inputs from self.inputs
        for key in dynamic_inputs_to_remove:
            del self.inputs[key]

        # Now destroy all widgets in the dynamic frame
        for widget in self.dynamic_frame.winfo_children():
            widget.destroy()

        plate_type = self.plate_type.get()
        self.update_image(plate_type)
        if plate_type == "Rectangular Flat":
            self.add_rectangular_flat_inputs()
        elif plate_type in ["Rectangular Curved Inner", "Rectangular Curved Outer"]:
            self.add_rectangular_curved_inputs()
        elif plate_type in ["Common Floor", "Basemat"]:
            self.add_circular_inputs()

    def add_rectangular_flat_inputs(self):
        inputs = [
            ("Plate Height", " [H]", "mm", ""),
            ("Plate Width", " [L]", "mm", ""),
            ("Datum Distance", " [D]", "mm", ""),
            ("Diaphragm Spacing", " [A]", "mm", ""),
            ("Columns Per Stud Group", " [N]", "", ["1", "2"]),
            ("Column Group Spacing", " [G]", "mm", ""),            
        ]
        self.add_dynamic_inputs(inputs)

    def add_rectangular_curved_inputs(self):
        inputs = [
            ("Radius", " [R]", "mm", ""),
            ("Plate Height", " [H]", "mm", ""),
            ("Plate Width Angle", " [L]", "degrees", ""),
            ("Datum Angle", " [D]", "degrees", ""),            
            ("Diaphragm Spacing", " [A]", "degrees", ""),
        ]
        self.add_dynamic_inputs(inputs)

    def add_circular_inputs(self):
        inputs = [
            ("Inner Radius", " [R1]", "mm", ""),
            ("Outer Radius", " [R2]", "mm", ""),
            ("Diaphragm Spacing", " [A]", "degrees", ""),
        ]
        self.add_dynamic_inputs(inputs)

    def add_dynamic_inputs(self, inputs):
        for i, (label, tag, unit, options) in enumerate(inputs):
            ttk.Label(self.dynamic_frame, width=25, text=f"{label}{tag}:").grid(row=i, column=0, padx=5, pady=5, sticky="w")
            if options:
                self.inputs[label] = ttk.Combobox(self.dynamic_frame, width=25, values=options)
            else:
                self.inputs[label] = ttk.Entry(self.dynamic_frame, width=27)
            self.inputs[label].grid(row=i, column=1, padx=5, pady=5, sticky="w")
            if unit:
                ttk.Label(self.dynamic_frame, text=unit).grid(row=i, column=2, padx=5, pady=5, sticky="w")    

    def browse_save_location(self):
        folder_selected = filedialog.askdirectory()
        self.save_location.set(folder_selected)

    def create_image_frame(self):
        self.image_frame = ttk.Frame(self.main_frame)
        self.image_frame.place(x=450, y=40)
        self.image_label = ttk.Label(self.image_frame)
        self.image_label.pack()

    def update_image(self, plate_type):
        image_path = f"images/{plate_type.lower().replace(' ', '_')}.png"
        try:
            image = Image.open(resource_path(image_path))
            image = image.resize((300, 300), Image.Resampling.LANCZOS)  # Adjust size as needed
            photo = ImageTk.PhotoImage(image)
            self.image_label.config(image=photo)
            self.image_label.image = photo  # Keep a reference
        except FileNotFoundError:
            self.image_label.config(image='')
            print(f"Image not found: {image_path}")    

    def generate_files(self):
#        if not self.validate_inputs():
#            return

        save_location = self.save_location.get()
        part_number = self.inputs["Part Number"].get()
        
        if not save_location or not part_number:
            messagebox.showerror("Error", "Save location and Part Number are required.")
            return

        input_data = self.get_input_data()
        coordinates = self.calculate_stud_positions(input_data)

        base_filename = f"{save_location}/{part_number}"
        
        self.save_json_file(f"{base_filename}.stud", input_data)
        self.save_csv_file(f"{base_filename}.csv", input_data, coordinates)
        self.create_step_file(f"{base_filename}.step", coordinates)

        messagebox.showinfo("Success", "Files generated successfully.")

    def validate_inputs(self):
        for key, widget in self.inputs.items():
            if isinstance(widget, ttk.Entry) and not widget.get():
                messagebox.showerror("Error", f"{key} is required.")
                return False
            elif isinstance(widget, ttk.Combobox) and not widget.get():
                messagebox.showerror("Error", f"{key} must be selected.")
                return False
        return True

    def get_input_data(self):
        data = {"Plate Type": self.plate_type.get()}
        for key, widget in self.inputs.items():
            data[key] = widget.get()

        # Add omission rules to input data
        data["Omission Rules"] = [
            {
                "Starting Column": inputs[0].get(),
                "Starting Row": inputs[1].get(),
                "Ending Column": inputs[2].get(),
                "Ending Row": inputs[3].get()
            }
            for _, inputs in self.omission_rules
        ]

        # Add Save Location to the data
        data["Save Location"] = self.save_location.get()
        
        return data

    def add_omission_rule(self):
        rule_frame = ttk.Frame(self.omission_frame)
        rule_frame.grid(row=len(self.omission_rules) + 1, column=0, padx=5, pady=5, sticky="w")

        labels = ["Starting Column", "Starting Row", "Ending Column", "Ending Row"]
        rule_inputs = []
        for i, label in enumerate(labels):
            ttk.Label(rule_frame, text=f"{label}:").grid(row=0, column=i*2, padx=5, pady=5, sticky="w")
            entry = ttk.Entry(rule_frame, width=5)
            entry.grid(row=0, column=i*2+1, padx=5, pady=5, sticky="w")
            rule_inputs.append(entry)

        delete_button = ttk.Button(rule_frame, text="Delete", command=lambda: self.delete_omission_rule(rule_frame))
        delete_button.grid(row=0, column=8, padx=5, pady=5)

        self.omission_rules.append((rule_frame, rule_inputs))

    def delete_omission_rule(self, rule_frame):
        index = next(i for i, (frame, _) in enumerate(self.omission_rules) if frame == rule_frame)
        rule_frame.destroy()
        del self.omission_rules[index]

        # Shift up remaining rules
        for i, (frame, _) in enumerate(self.omission_rules[index:], start=index):
            frame.grid(row=i + 1, column=0, padx=5, pady=5, sticky="w")

    def filter_coordinates(self, coordinates):
        # Apply omission rules
        omitted_coordinates = []
        for _, rule_inputs in self.omission_rules:
            start_col, start_row, end_col, end_row = [int(entry.get()) for entry in rule_inputs]
            for col in range(start_col, end_col + 1):
                for row in range(start_row, end_row + 1):
                    omitted_coordinates.append((col, row))

        # Filter out omitted coordinates
        filtered_coordinates = [
            coord for coord in coordinates if (coord[0], coord[1]) not in omitted_coordinates
        ]
        return filtered_coordinates
    
    def calculate_stud_positions(self, input_data):
        
        plate_type = input_data["Plate Type"]
        stud_spacing = float(input_data["Stud Spacing"])
        stud_start_height = float(input_data["Stud Start Height"])
        datum_thickness = float(input_data["Datum Thickness"])
        datum_height = 38.1
        datum_alignment = input_data["Datum Alignment"]
        stud_diameter = 19.05

        coordinates = []
        
        if plate_type == "Rectangular Flat":
            plate_height = float(input_data["Plate Height"])
            plate_width = float(input_data["Plate Width"])            
            datum_distance = float((input_data["Datum Distance"]))
            diaphragm_spacing = float(input_data["Diaphragm Spacing"])            
            col_per_group = int(input_data["Columns Per Stud Group"])
            stud_start_y_edge = datum_distance - (stud_diameter/2)
            stud_stop_y_edge = datum_distance - plate_width + (stud_diameter/2)
            if col_per_group == 2:
                group_spacing = float(input_data["Column Group Spacing"])                
            if datum_alignment == "Studs":
                stud_start_y = int(stud_start_y_edge/diaphragm_spacing)*diaphragm_spacing
                stud_stop_y = (int(stud_stop_y_edge/diaphragm_spacing)*diaphragm_spacing) - .1
            elif datum_alignment == "Diaphragm":
                stud_start_y = (int((stud_start_y_edge - (diaphragm_spacing/2))/diaphragm_spacing)*diaphragm_spacing) + (diaphragm_spacing/2)                
                stud_stop_y = (int((stud_stop_y_edge + (diaphragm_spacing/2))/diaphragm_spacing)*diaphragm_spacing) - (diaphragm_spacing/2) - .1               
            if col_per_group == 2:
                if (stud_start_y + diaphragm_spacing - (group_spacing/2)) < stud_start_y_edge:
                    stud_start_y = stud_start_y + diaphragm_spacing
                if (stud_stop_y - diaphragm_spacing + (group_spacing/2)) > stud_stop_y_edge:
                    stud_stop_y = stud_stop_y - diaphragm_spacing
            
            column = 0
            
            for y in arange(stud_start_y,stud_stop_y,-diaphragm_spacing):            
                row = 0
                if col_per_group == 1:
                    column +=1
                    for z in arange(stud_start_height, plate_height, stud_spacing):
                        row += 1
                        coordinates.append((column, row, -datum_thickness, y, z + datum_height, 0)) 
                elif col_per_group == 2:
                    if y + (group_spacing/2) <= (stud_start_y_edge):
                        column +=1
                        for z in arange(stud_start_height, plate_height, stud_spacing):                        
                            row += 1                        
                            coordinates.append((column, row, -datum_thickness, y + (group_spacing/2), z + datum_height, 0))                   
                    if y - (group_spacing/2) >= (stud_stop_y_edge):
                        row = 0
                        column += 1
                        for z in arange(stud_start_height, plate_height, stud_spacing):                        
                            row += 1
                            coordinates.append((column, row, -datum_thickness, y - (group_spacing/2), z + datum_height, 0))  
        
        elif plate_type in ["Rectangular Curved Inner", "Rectangular Curved Outer"]:
            radius = float(input_data["Radius"])                        
            datum_angle = math.radians(float(input_data["Datum Angle"]))
            plate_width_angle = math.radians(float(input_data["Plate Width Angle"]))
            plate_height = float(input_data["Plate Height"])
            diaphragm_spacing = math.radians(float(input_data["Diaphragm Spacing"]))
            
            if datum_alignment == "Studs":
                stud_start = int(datum_angle/diaphragm_spacing)*diaphragm_spacing
            if datum_alignment == "Diaphragm":
                start_check = int(datum_angle/(diaphragm_spacing/2))
                if (start_check % 2) == 0:
                    start_check = start_check - 1
                stud_start = start_check*(diaphragm_spacing/2)

            if plate_type == "Rectangular Curved Outer":
                stud_radius = radius - datum_thickness                
                column = 0
                for angle in arange(stud_start, datum_angle - plate_width_angle, -diaphragm_spacing):
                    row = 0
                    column += 1
                    for z in arange(stud_start_height, plate_height-(stud_diameter/2), stud_spacing):
                        row += 1
                        x = -datum_thickness - (stud_radius * (1-math.cos(angle)))
                        y = stud_radius * math.sin(angle)
                        coordinates.append((column, row, x, y, z + datum_height, math.degrees(angle)))
            
            elif plate_type == "Rectangular Curved Inner":
                stud_radius = radius + datum_thickness
                column = 0
                for angle in arange(stud_start, datum_angle - plate_width_angle, -diaphragm_spacing):
                    row = 0
                    column += 1
                    for z in arange(stud_start_height, plate_height-(stud_diameter/2), stud_spacing):
                        row += 1
                        x = -datum_thickness + (stud_radius * (1-math.cos(angle)))
                        y = stud_radius * math.sin(angle)
                        coordinates.append((column, row, x, y, z + datum_height, -math.degrees(angle)))
        
        elif plate_type in ["Common Floor", "Basemat"]:
            inner_radius = float(input_data["Inner Radius"])
            outer_radius = float(input_data["Outer Radius"])
            diaphragm_spacing = math.radians(float(input_data["Diaphragm Spacing"]))
            
            for angle in arange(0, 2*math.pi, diaphragm_spacing):
                for r in arange(inner_radius, outer_radius, stud_spacing):
                    x = r * math.cos(angle)
                    y = r * math.sin(angle)
                    z = 38.1  # Starting Z at 38.1 as per specification
                    coordinates.append((x, y, z, math.degrees(angle)))
        
        filtered_coordinates = self.filter_coordinates(coordinates)

        return filtered_coordinates        

    def save_json_file(self, filename, data):
        with open(filename, 'w') as f:
            json.dump(data, f, indent=4)

    def load_json_file(self):
        file_path = filedialog.askopenfilename(filetypes=[("STUD files", "*.stud")])
        if not file_path:
            return

        with open(file_path, 'r') as f:
            data = json.load(f)

        # Set plate type
        if "Plate Type" in data:
            self.plate_type.set(data["Plate Type"])
            self.update_form()

        # Populate common inputs
        for key, widget in self.inputs.items():
            if key in data:
                if isinstance(widget, ttk.Combobox):
                    widget.set(data[key])
                elif isinstance(widget, ttk.Entry):
                    widget.delete(0, tk.END)
                    widget.insert(0, data[key])

        # Populate dynamic inputs
        for key in data:
            if key not in self.inputs and key != "Plate Type" and key != "Omission Rules" and key != "Save Location":
                if key in self.inputs:
                    self.inputs[key].delete(0, tk.END)
                    self.inputs[key].insert(0, data[key])

        # Set Save Location
        if "Save Location" in data:
            self.save_location.set(data["Save Location"])

        # Load Omission Rules
        if "Omission Rules" in data:
            # Clear existing rules
            for rule_frame, _ in self.omission_rules:
                rule_frame.destroy()
            self.omission_rules.clear()

            # Add loaded rules
            for rule in data["Omission Rules"]:
                self.add_omission_rule()
                for i, key in enumerate(["Starting Column", "Starting Row", "Ending Column", "Ending Row"]):
                    self.omission_rules[-1][1][i].insert(0, rule[key])

    def save_csv_file(self, filename, input_data, coordinates):
        with open(filename, 'w', newline='') as f:
            writer = csv.writer(f)
            writer.writerow(["Type", "Index", "Field", "Value"])
            writer.writerow(["Part_Name", "", "", input_data["Part Description"]])
            writer.writerow(["Part_Number", "", "", input_data["Part Number"]])
            plate_type_text = input_data["Plate Type"]
            plate_type = 0
            if plate_type_text == "Rectangular Flat":
                plate_type = 1
            elif plate_type_text == "Rectangular Curved Inner":
                plate_type = 2
            elif plate_type_text == "Rectangular Curved Outer":
                plate_type = 3
            elif plate_type_text == "Common Floor":
                plate_type = 4
            elif plate_type_text == "Basemat":
                plate_type = 5
            writer.writerow(["Assembly_Type", "", "", plate_type])
            writer.writerow(["Total_Studs", "", "", len(coordinates)])
            
            for i, (col, row, x, y, z, roll) in enumerate(coordinates, start=1):
                writer.writerow(["Stud", i, "X_Pos", round(x)])
                writer.writerow(["Stud", i, "Y_Pos", round(y)])
                writer.writerow(["Stud", i, "Z_Pos", round(z)])
                writer.writerow(["Stud", i, "Roll", round(roll*10)])

    def create_stud_shape(self):
        radius = 19.05 / 2  # Diameter is 19.05mm
        height = 150  # Assuming a default height, adjust as needed
        axis = gp_Ax2(gp_Pnt(0, 0, 0), gp_Dir(-1, 0, 0))
        return BRepPrimAPI_MakeCylinder(axis, radius, height).Shape()
    
    def create_step_file(self, filename, coordinates):        
        # Create the base stud shape
        stud_shape = self.create_stud_shape()

        # Create a STEP writer
        step_writer = STEPControl_Writer()

        # Set the name for the stud shape
        Interface_Static.SetCVal("write.step.product.name", "Stud")

        # Add the base stud shape to the STEP file
        step_writer.Transfer(stud_shape, STEPControl_AsIs)

        compound = TopoDS_Compound()
        builder = BRep_Builder()
        builder.MakeCompound(compound)

        # Create instances of the stud for each coordinate
        for i, (col, row, x, y, z, roll) in enumerate(coordinates):
            # Create a transformation
            transform = gp_Trsf()
            
            # Rotate
            roll_rad = math.radians(roll)
            rotation_axis = gp_Ax1(gp_Pnt(0, 0, 0), gp_Dir(0, 0, 1))
            transform.SetRotation(rotation_axis, roll_rad)
            
            # Translate
            transform.SetTranslationPart(gp_Vec(x, y, z))
            
            # Apply the transformation to create a new instance
            stud_instance = BRepBuilderAPI_Transform(stud_shape, transform).Shape()
            
            # Set a unique name for each instance
            Interface_Static.SetCVal("write.step.product.name", f"Stud_Instance_{i+1}")
            
            # Add the instance to the STEP file
            builder.Add(compound,stud_instance)
            #step_writer.Transfer(stud_instance, STEPControl_AsIs)

        # Write the STEP file
        step_writer.Transfer(compound, STEPControl_AsIs)
        status = step_writer.Write(filename)

        if status == 0:
            print(f"STEP file '{filename}' created successfully.")
        else:
            print("Error creating STEP file.")

def resource_path(relative_path):    
    # Attempt the 'temp' path used for .exe
    try:       
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")

    return os.path.join(base_path, relative_path)

def main():
    root = tk.Tk()
    root.iconbitmap(default=resource_path("SPG.ico"))
    app = StudPlateGenerator(root)
    root.mainloop()

if __name__ == "__main__":
    main()