import { Routes, Route } from "react-router-dom";
import Dashboard from "../components/dashboard/Dashboard";
import ClassDetail from "../components/class/ClassDetail";

function AppRoutes() {
  return (
    <Routes>
      <Route path="/" element={<Dashboard />} />
      <Route path="/class/:id" element={<ClassDetail />} />
    </Routes>
  );
}

export default AppRoutes;