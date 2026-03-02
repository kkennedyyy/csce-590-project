
const API_FUNC = "";

export async function getStudentDashboard() {
    const response = await fetch(`${API_FUNC}/GetStudentDashboard`); 

    if (!response.ok) {
        throw new Error("Failed to retrieve student");
    }

    return response.json();
    
}