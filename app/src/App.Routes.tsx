import React from "react";
import {Route, Routes} from "react-router-dom";
import MainLayout from "./components/Layouts/MainLayout";
import NotFound from "./components/Errors/NotFound";

function AppRoutes() {
    return (
        <Routes>
            <Route element={<MainLayout/>}>
                <Route path={"/"}>
                    <Route index element={<>Where in the world is Jackson? Find out here soon.</>}/>
                </Route>
                <Route path={"*"} element={<NotFound/>}/>
            </Route>
        </Routes>
    );
}

export default React.memo(AppRoutes);