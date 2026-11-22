import { Routes, Route } from 'react-router';
import Layout from './components/Layout';
import Dashboard from './pages/Dashboard';
import DocumentList from './pages/DocumentList';
import DocumentUpload from './pages/DocumentUpload';
import DocumentReview from './pages/DocumentReview';
import Templates from './pages/Templates';
import Metrics from './pages/Metrics';

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Dashboard />} />
        <Route path="documents" element={<DocumentList />} />
        <Route path="documents/:id" element={<DocumentReview />} />
        <Route path="upload" element={<DocumentUpload />} />
        <Route path="templates" element={<Templates />} />
        <Route path="metrics" element={<Metrics />} />
      </Route>
    </Routes>
  );
}
